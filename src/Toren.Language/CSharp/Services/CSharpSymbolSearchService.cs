using Toren.Language.CSharp.Contracts;
using Toren.Language.CSharp.Models;

namespace Toren.Language.CSharp.Services;

public sealed class CSharpSymbolSearchService : ICSharpSymbolSearchService
{
    public IReadOnlyList<CSharpWorkspaceSymbol> Search(
        IReadOnlyList<CSharpWorkspaceSymbol> symbols,
        string query,
        int maxResults = 75)
    {
        ArgumentNullException.ThrowIfNull(symbols);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);

        var normalizedQuery = query.Trim();
        if (normalizedQuery.Length == 0)
        {
            return symbols.Take(maxResults).ToArray();
        }

        return symbols
            .Select(symbol => new RankedSymbol(symbol, Score(symbol, normalizedQuery)))
            .Where(candidate => candidate.Score is not null)
            .OrderBy(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Symbol.Name.Length)
            .ThenBy(candidate => candidate.Symbol.Name, StringComparer.OrdinalIgnoreCase)
            .Take(maxResults)
            .Select(candidate => candidate.Symbol)
            .ToArray();
    }

    private static int? Score(CSharpWorkspaceSymbol symbol, string query)
    {
        if (symbol.Name.Equals(query, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (symbol.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 10 + symbol.Name.Length - query.Length;
        }

        var nameIndex = symbol.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (nameIndex >= 0)
        {
            return 30 + nameIndex;
        }

        if (!string.IsNullOrWhiteSpace(symbol.ContainerName))
        {
            var containerIndex = symbol.ContainerName.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (containerIndex >= 0)
            {
                return 60 + containerIndex;
            }
        }

        var fuzzyPenalty = GetSubsequencePenalty(symbol.Name, query);
        if (fuzzyPenalty is not null)
        {
            return 100 + fuzzyPenalty.Value;
        }

        return null;
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

            penalty += previousMatch < 0
                ? candidateIndex
                : candidateIndex - previousMatch - 1;
            previousMatch = candidateIndex;
            queryIndex++;
        }

        return queryIndex == query.Length ? penalty : null;
    }

    private sealed record RankedSymbol(CSharpWorkspaceSymbol Symbol, int? Score);
}
