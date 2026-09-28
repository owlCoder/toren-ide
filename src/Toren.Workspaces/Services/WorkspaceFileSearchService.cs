using Toren.Workspaces.Contracts;
using Toren.Workspaces.Models;

namespace Toren.Workspaces.Services;

public sealed class WorkspaceFileSearchService : IWorkspaceFileSearchService
{
    public IReadOnlyList<WorkspaceFileEntry> Search(
        IReadOnlyList<WorkspaceFileEntry> files,
        string query,
        int maxResults = 50)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);

        var normalizedQuery = query.Trim();
        if (normalizedQuery.Length == 0)
        {
            return files.Take(maxResults).ToArray();
        }

        return files
            .Select(file => new RankedFile(file, Score(file, normalizedQuery)))
            .Where(candidate => candidate.Score is not null)
            .OrderBy(candidate => candidate.Score)
            .ThenBy(candidate => candidate.File.Name.Length)
            .ThenBy(candidate => candidate.File.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Take(maxResults)
            .Select(candidate => candidate.File)
            .ToArray();
    }

    private static int? Score(WorkspaceFileEntry file, string query)
    {
        if (file.Name.Equals(query, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (file.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 10 + file.Name.Length - query.Length;
        }

        var nameIndex = file.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (nameIndex >= 0)
        {
            return 30 + nameIndex;
        }

        if (file.RelativePath.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 50 + file.RelativePath.Length - query.Length;
        }

        var pathIndex = file.RelativePath.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (pathIndex >= 0)
        {
            return 70 + pathIndex;
        }

        var fuzzyPenalty = GetSubsequencePenalty(file.Name, query);
        if (fuzzyPenalty is not null)
        {
            return 100 + fuzzyPenalty.Value;
        }

        fuzzyPenalty = GetSubsequencePenalty(file.RelativePath, query);
        return fuzzyPenalty is null ? null : 150 + fuzzyPenalty.Value;
    }

    private static int? GetSubsequencePenalty(string candidate, string query)
    {
        var queryIndex = 0;
        var previousMatch = -1;
        var penalty = 0;

        for (var candidateIndex = 0; candidateIndex < candidate.Length && queryIndex < query.Length; candidateIndex++)
        {
            if (char.ToUpperInvariant(candidate[candidateIndex]) != char.ToUpperInvariant(query[queryIndex]))
            {
                continue;
            }

            if (previousMatch >= 0)
            {
                penalty += candidateIndex - previousMatch - 1;
            }
            else
            {
                penalty += candidateIndex;
            }

            previousMatch = candidateIndex;
            queryIndex++;
        }

        return queryIndex == query.Length ? penalty : null;
    }

    private sealed record RankedFile(WorkspaceFileEntry File, int? Score);
}
