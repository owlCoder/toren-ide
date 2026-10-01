using Toren.Core.IO;

namespace Toren.App.Search.Models;

public static class WorkspaceTextSearchPresentation
{
    private static readonly StringComparer PathComparer = FileSystemPath.Comparer;

    public static IReadOnlyList<WorkspaceTextSearchDisplayItem> Build(
        IReadOnlyList<WorkspaceTextSearchResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        if (results.Count == 0)
        {
            return [];
        }

        var groups = new List<SearchResultGroup>();
        var groupsByPath = new Dictionary<string, SearchResultGroup>(PathComparer);
        foreach (var result in results)
        {
            if (!groupsByPath.TryGetValue(result.FilePath, out var group))
            {
                group = new SearchResultGroup(result.RelativePath);
                groupsByPath.Add(result.FilePath, group);
                groups.Add(group);
            }

            group.Results.Add(result);
        }

        var displayItems = new List<WorkspaceTextSearchDisplayItem>(results.Count + groups.Count);
        foreach (var group in groups)
        {
            displayItems.Add(new WorkspaceTextSearchDisplayItem(
                IsHeader: true,
                group.RelativePath,
                group.Results.Count,
                Result: null));
            displayItems.AddRange(group.Results.Select(result => new WorkspaceTextSearchDisplayItem(
                IsHeader: false,
                group.RelativePath,
                group.Results.Count,
                result)));
        }

        return displayItems;
    }

    private sealed class SearchResultGroup(string relativePath)
    {
        public string RelativePath { get; } = relativePath;

        public List<WorkspaceTextSearchResult> Results { get; } = [];
    }
}
