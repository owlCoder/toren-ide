using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Toren.Language.CSharp.Models;

namespace Toren.Language.CSharp.Services;

/// <summary>
/// What consecutive editor requests for the same project have in common: parsed syntax trees
/// of unchanged documents and the incremental state of its source generators. Reusing the
/// trees is also what lets generators skip work, because they track inputs by tree identity.
/// Only a few recently used projects are kept, and every reused piece is checked against the
/// current text or file state first.
/// </summary>
internal sealed class RoslynProjectState
{
    private const int MaximumProjects = 4;

    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
    private static readonly Dictionary<string, RoslynProjectState> Projects = new(PathComparer);

    private readonly Lock _gate = new();
    private long _lastUsed;
    private CSharpParseOptions? _parseOptions;
    private Dictionary<string, CachedTree> _trees = new(PathComparer);
    private string? _generatorStamp;
    private ImmutableArray<ISourceGenerator> _generators = [];
    private GeneratorDriver? _driver;
    private Dictionary<string, CachedAdditionalText> _additionalTexts = new(PathComparer);
    private string _analyzerConfigStamp = string.Empty;
    private ProjectAnalyzerConfigOptionsProvider? _analyzerConfigOptions;

    /// <summary>Returns the state of a project, or <see langword="null"/> when it has no identity.</summary>
    public static RoslynProjectState? For(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return null;
        }

        lock (Projects)
        {
            if (!Projects.TryGetValue(projectPath, out var state))
            {
                if (Projects.Count >= MaximumProjects)
                {
                    Projects.Remove(Projects.MinBy(static project => project.Value._lastUsed).Key);
                }

                Projects.Add(projectPath, state = new RoslynProjectState());
            }

            state._lastUsed = Stopwatch.GetTimestamp();
            return state;
        }
    }

    /// <summary>Parses the documents, reusing the tree of every document whose text is unchanged.</summary>
    public SyntaxTree[] GetSyntaxTrees(
        IReadOnlyList<CSharpSourceDocument> documents,
        CSharpParseOptions parseOptions,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (!parseOptions.Equals(_parseOptions))
            {
                _parseOptions = parseOptions;
                _trees = new Dictionary<string, CachedTree>(PathComparer);
                _driver = null;
            }

            var current = new Dictionary<string, CachedTree>(documents.Count, PathComparer);
            var trees = new SyntaxTree[documents.Count];
            for (var index = 0; index < trees.Length; index++)
            {
                var document = documents[index];
                if (!_trees.TryGetValue(document.Path, out var cached)
                    || !string.Equals(cached.Text, document.Text, StringComparison.Ordinal)
                    || !string.Equals(cached.Tree.FilePath, document.Path, StringComparison.Ordinal))
                {
                    cached = new CachedTree(
                        document.Text,
                        CSharpSyntaxTree.ParseText(
                            document.Text,
                            options: parseOptions,
                            path: document.Path,
                            cancellationToken: cancellationToken));
                }

                trees[index] = cached.Tree;
                current[document.Path] = cached;
            }

            // Only the documents of the latest request are kept.
            _trees = current;
            return trees;
        }
    }

    /// <summary>Runs the project's source generators, continuing from their previous run.</summary>
    public CSharpCompilation RunGenerators(
        CSharpCompilation compilation,
        CSharpSemanticContext context,
        CSharpParseOptions parseOptions,
        CancellationToken cancellationToken)
    {
        GeneratorDriver? driver;
        lock (_gate)
        {
            // Generator assemblies are loaded once and again only when one of them is rewritten.
            var generatorStamp = GetFileStamp(context.AnalyzerPaths);
            if (!string.Equals(generatorStamp, _generatorStamp, StringComparison.Ordinal))
            {
                _generatorStamp = generatorStamp;
                _generators = RoslynAnalyzerLoader.LoadGenerators(context.AnalyzerPaths);
                _driver = null;
            }

            if (_generators.IsDefaultOrEmpty)
            {
                return compilation;
            }

            var additionalTexts = GetAdditionalTexts(context.AdditionalFilePaths);
            var analyzerConfigStamp = GetFileStamp(context.AnalyzerConfigPaths);
            var analyzerConfigChanged = _analyzerConfigOptions is null
                || !string.Equals(analyzerConfigStamp, _analyzerConfigStamp, StringComparison.Ordinal);
            if (analyzerConfigChanged)
            {
                _analyzerConfigStamp = analyzerConfigStamp;
                _analyzerConfigOptions = new ProjectAnalyzerConfigOptionsProvider(context.AnalyzerConfigPaths);
            }

            driver = _driver is null
                ? CSharpGeneratorDriver.Create(_generators, additionalTexts, parseOptions, _analyzerConfigOptions)
                : _driver.ReplaceAdditionalTexts(additionalTexts);
            if (_driver is not null && analyzerConfigChanged)
            {
                driver = driver.WithUpdatedAnalyzerConfigOptions(_analyzerConfigOptions!);
            }
        }

        // Generators run outside the lock; overlapping requests may both run them, and either
        // resulting state is a valid starting point for the next request.
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var generated, out _, cancellationToken);
        lock (_gate)
        {
            if (parseOptions.Equals(_parseOptions))
            {
                _driver = driver;
            }
        }

        return (CSharpCompilation)generated;
    }

    private ImmutableArray<AdditionalText> GetAdditionalTexts(IReadOnlyList<string> paths)
    {
        var current = new Dictionary<string, CachedAdditionalText>(paths.Count, PathComparer);
        var texts = ImmutableArray.CreateBuilder<AdditionalText>(paths.Count);
        foreach (var path in paths)
        {
            var file = new FileInfo(path);
            if (!file.Exists || current.ContainsKey(path))
            {
                continue;
            }

            // Generators treat an unchanged instance as unchanged content, so a file gets a
            // new instance whenever it has been written.
            if (!_additionalTexts.TryGetValue(path, out var cached)
                || cached.LastWriteTimeUtc != file.LastWriteTimeUtc
                || cached.Length != file.Length)
            {
                cached = new CachedAdditionalText(file.LastWriteTimeUtc, file.Length, new ProjectAdditionalText(path));
            }

            current.Add(path, cached);
            texts.Add(cached.Text);
        }

        _additionalTexts = current;
        return texts.ToImmutable();
    }

    private static string GetFileStamp(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            return string.Empty;
        }

        var stamp = new System.Text.StringBuilder();
        foreach (var path in paths)
        {
            var file = new FileInfo(path);
            stamp.Append(path).Append('|');
            if (file.Exists)
            {
                stamp.Append(file.LastWriteTimeUtc.Ticks).Append('|').Append(file.Length);
            }

            stamp.Append('\n');
        }

        return stamp.ToString();
    }

    private sealed record CachedTree(string Text, SyntaxTree Tree);

    private sealed record CachedAdditionalText(DateTime LastWriteTimeUtc, long Length, AdditionalText Text);
}
