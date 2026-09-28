using System.IO.Enumeration;
using System.Text.RegularExpressions;
using Toren.App.Documents.Contracts;
using Toren.App.Search.Contracts;
using Toren.App.Search.Models;
using Toren.Core.Results;
using Toren.Workspaces.Contracts;

namespace Toren.App.Search.Services;

public sealed class WorkspaceTextSearchService(
    IWorkspaceFileProvider fileProvider,
    ITextDocumentStore documentStore) : IWorkspaceTextSearchService
{
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(500);

    private static readonly HashSet<string> SearchableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".csproj", ".sln", ".slnx", ".json", ".xml", ".props", ".targets",
        ".config", ".yml", ".yaml", ".md", ".txt", ".http", ".razor", ".cshtml",
        ".sh", ".ps1",
    };

    private static readonly HashSet<string> SearchableFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Dockerfile", ".editorconfig", ".gitignore", ".gitattributes", ".dockerignore",
    };

    private readonly IWorkspaceFileProvider _fileProvider = fileProvider
        ?? throw new ArgumentNullException(nameof(fileProvider));
    private readonly ITextDocumentStore _documentStore = documentStore
        ?? throw new ArgumentNullException(nameof(documentStore));

    public async Task<Result<IReadOnlyList<WorkspaceTextSearchResult>>> SearchAsync(
        string workspacePath,
        string query,
        WorkspaceTextSearchOptions options,
        IReadOnlyDictionary<string, string>? textOverrides = null,
        int maxResults = 200,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);

        var matcherResult = CreateMatcher(query, options);
        if (!matcherResult.IsSuccess)
        {
            return Result.Failure<IReadOnlyList<WorkspaceTextSearchResult>>(matcherResult.Error);
        }

        var files = await _fileProvider.GetFilesAsync(workspacePath, cancellationToken).ConfigureAwait(false);
        if (!files.IsSuccess)
        {
            return Result.Failure<IReadOnlyList<WorkspaceTextSearchResult>>(files.Error);
        }

        var overrides = NormalizeOverrides(textOverrides);
        var includePatterns = SplitPatterns(options.IncludePatterns);
        var excludePatterns = SplitPatterns(options.ExcludePatterns);
        var results = new List<WorkspaceTextSearchResult>(Math.Min(maxResults, 200));

        try
        {
            foreach (var file in files.Value)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsSearchable(file.Path)
                    || !MatchesPathFilters(file.RelativePath, includePatterns, excludePatterns))
                {
                    continue;
                }

                var fullPath = Path.GetFullPath(file.Path);
                string? text = null;
                if (overrides is not null)
                {
                    overrides.TryGetValue(fullPath, out text);
                }

                if (text is null)
                {
                    var loaded = await _documentStore.LoadAsync(fullPath, cancellationToken).ConfigureAwait(false);
                    if (!loaded.IsSuccess)
                    {
                        continue;
                    }

                    text = loaded.Value.Text;
                }

                AddMatches(file.RelativePath, fullPath, text, matcherResult.Value, results, maxResults);
                if (results.Count >= maxResults)
                {
                    break;
                }
            }
        }
        catch (RegexMatchTimeoutException)
        {
            return Result.Failure<IReadOnlyList<WorkspaceTextSearchResult>>(
                OperationError.Create(
                    "search.regex.timeout",
                    "The regular expression took too long to evaluate. Narrow the expression and try again."));
        }

        return Result.Success<IReadOnlyList<WorkspaceTextSearchResult>>(results);
    }

    private static Result<SearchMatcher> CreateMatcher(string query, WorkspaceTextSearchOptions options)
    {
        if (!options.UseRegularExpression && !options.MatchWholeWord)
        {
            var comparison = options.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            return Result.Success(new SearchMatcher(query, comparison, null));
        }

        var pattern = options.UseRegularExpression ? query : Regex.Escape(query);
        if (options.MatchWholeWord)
        {
            pattern = $"(?<![\\p{{L}}\\p{{Nd}}_])(?:{pattern})(?![\\p{{L}}\\p{{Nd}}_])";
        }

        var regexOptions = RegexOptions.CultureInvariant;
        if (!options.MatchCase)
        {
            regexOptions |= RegexOptions.IgnoreCase;
        }

        try
        {
            return Result.Success(new SearchMatcher(
                query,
                StringComparison.Ordinal,
                new Regex(pattern, regexOptions, RegexTimeout)));
        }
        catch (ArgumentException exception)
        {
            return Result.Failure<SearchMatcher>(
                OperationError.Create(
                    "search.regex.invalid",
                    $"Invalid regular expression: {exception.Message}"));
        }
    }

    private static Dictionary<string, string>? NormalizeOverrides(
        IReadOnlyDictionary<string, string>? textOverrides)
    {
        if (textOverrides is null || textOverrides.Count == 0)
        {
            return null;
        }

        var normalized = new Dictionary<string, string>(PathComparer);
        foreach (var pair in textOverrides)
        {
            normalized[Path.GetFullPath(pair.Key)] = pair.Value;
        }

        return normalized;
    }

    private static bool IsSearchable(string path)
    {
        var fileName = Path.GetFileName(path);
        return SearchableFileNames.Contains(fileName)
            || SearchableExtensions.Contains(Path.GetExtension(path));
    }

    private static string[] SplitPatterns(string patterns) =>
        string.IsNullOrWhiteSpace(patterns)
            ? []
            : patterns.Split(
                [';', ','],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool MatchesPathFilters(
        string relativePath,
        string[] includePatterns,
        string[] excludePatterns)
    {
        var normalizedPath = relativePath.Replace('\\', '/');
        if (includePatterns.Length > 0 && !MatchesAnyPattern(normalizedPath, includePatterns))
        {
            return false;
        }

        return excludePatterns.Length == 0 || !MatchesAnyPattern(normalizedPath, excludePatterns);
    }

    private static bool MatchesAnyPattern(string relativePath, string[] patterns)
    {
        var fileName = Path.GetFileName(relativePath);
        foreach (var pattern in patterns)
        {
            var normalizedPattern = pattern.Replace('\\', '/');
            if (FileSystemName.MatchesSimpleExpression(normalizedPattern, relativePath, ignoreCase: true)
                || (!normalizedPattern.Contains('/')
                    && FileSystemName.MatchesSimpleExpression(normalizedPattern, fileName, ignoreCase: true)))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddMatches(
        string relativePath,
        string filePath,
        string text,
        SearchMatcher matcher,
        List<WorkspaceTextSearchResult> results,
        int maxResults)
    {
        using var reader = new StringReader(text);
        var lineNumber = 0;
        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            if (matcher.Regex is not null)
            {
                AddRegexMatches(relativePath, filePath, line, lineNumber, matcher.Regex, results, maxResults);
            }
            else
            {
                AddLiteralMatches(
                    relativePath,
                    filePath,
                    line,
                    lineNumber,
                    matcher.Query,
                    matcher.Comparison,
                    results,
                    maxResults);
            }

            if (results.Count >= maxResults)
            {
                return;
            }
        }
    }

    private static void AddLiteralMatches(
        string relativePath,
        string filePath,
        string line,
        int lineNumber,
        string query,
        StringComparison comparison,
        List<WorkspaceTextSearchResult> results,
        int maxResults)
    {
        var searchOffset = 0;
        while (searchOffset <= line.Length - query.Length)
        {
            var index = line.IndexOf(query, searchOffset, comparison);
            if (index < 0)
            {
                return;
            }

            AddResult(relativePath, filePath, line, lineNumber, index, results);
            if (results.Count >= maxResults)
            {
                return;
            }

            searchOffset = index + query.Length;
        }
    }

    private static void AddRegexMatches(
        string relativePath,
        string filePath,
        string line,
        int lineNumber,
        Regex regex,
        List<WorkspaceTextSearchResult> results,
        int maxResults)
    {
        foreach (Match match in regex.Matches(line))
        {
            AddResult(relativePath, filePath, line, lineNumber, match.Index, results);
            if (results.Count >= maxResults)
            {
                return;
            }
        }
    }

    private static void AddResult(
        string relativePath,
        string filePath,
        string line,
        int lineNumber,
        int index,
        List<WorkspaceTextSearchResult> results)
    {
        results.Add(new WorkspaceTextSearchResult(
            filePath,
            relativePath,
            lineNumber,
            index + 1,
            CreatePreview(line)));
    }

    private static string CreatePreview(string line)
    {
        const int maxPreviewLength = 180;
        var preview = line.Trim();
        return preview.Length <= maxPreviewLength
            ? preview
            : $"{preview[..(maxPreviewLength - 1)]}…";
    }

    private sealed record SearchMatcher(
        string Query,
        StringComparison Comparison,
        Regex? Regex);
}
