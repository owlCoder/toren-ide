using Toren.App.Editor.Models;

namespace Toren.App.Editor.Services;

public static class TextSearchService
{
    public static IReadOnlyList<TextSearchMatch> FindAll(
        string text,
        string pattern,
        bool matchCase)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(pattern);

        if (pattern.Length == 0 || text.Length == 0)
        {
            return [];
        }

        var comparison = matchCase
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        var matches = new List<TextSearchMatch>();
        var searchStart = 0;

        while (searchStart <= text.Length - pattern.Length)
        {
            var matchOffset = text.IndexOf(pattern, searchStart, comparison);
            if (matchOffset < 0)
            {
                break;
            }

            matches.Add(new TextSearchMatch(matchOffset, pattern.Length));
            searchStart = matchOffset + pattern.Length;
        }

        return matches;
    }
}
