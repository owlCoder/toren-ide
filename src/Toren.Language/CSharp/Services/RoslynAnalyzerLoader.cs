using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Toren.Language.CSharp.Services;

internal static class RoslynAnalyzerLoader
{
    public static ImmutableArray<DiagnosticAnalyzer> Load(IReadOnlyList<string> analyzerPaths)
    {
        ArgumentNullException.ThrowIfNull(analyzerPaths);

        if (analyzerPaths.Count == 0)
        {
            return [];
        }

        var loader = new AnalyzerAssemblyLoader();
        var analyzers = ImmutableArray.CreateBuilder<DiagnosticAnalyzer>();

        foreach (var path in analyzerPaths)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(path);
            loader.AddDependencyLocation(fullPath);

            try
            {
                var reference = new AnalyzerFileReference(fullPath, loader);
                analyzers.AddRange(reference.GetAnalyzers(LanguageNames.CSharp));
            }
            catch (IOException)
            {
                // A stale or unloadable analyzer path should not take down language services.
            }
            catch (UnauthorizedAccessException)
            {
                // The project remains usable even when an analyzer cannot be read.
            }
            catch (BadImageFormatException)
            {
                // Ignore native or otherwise invalid analyzer assemblies.
            }
        }

        return analyzers
            .DistinctBy(analyzer => analyzer.GetType().AssemblyQualifiedName, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    public static ImmutableArray<ISourceGenerator> LoadGenerators(IReadOnlyList<string> paths)
    {
        var loader = new AnalyzerAssemblyLoader();
        var generators = ImmutableArray.CreateBuilder<ISourceGenerator>();
        foreach (var path in paths.Where(File.Exists).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                generators.AddRange(new AnalyzerFileReference(Path.GetFullPath(path), loader).GetGenerators(LanguageNames.CSharp));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (BadImageFormatException) { }
        }

        // Incremental generators all use the same wrapper type; preserve every generator.
        return generators.ToImmutable();
    }

    private sealed class AnalyzerAssemblyLoader : IAnalyzerAssemblyLoader
    {
        public void AddDependencyLocation(string fullPath)
        {
        }

        public Assembly LoadFromPath(string fullPath) => Assembly.LoadFrom(fullPath);
    }
}
