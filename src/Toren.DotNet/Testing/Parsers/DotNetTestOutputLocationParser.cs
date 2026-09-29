using System.Globalization;
using System.Text.RegularExpressions;
using Toren.DotNet.Testing.Models;

namespace Toren.DotNet.Testing.Parsers;

public static partial class DotNetTestOutputLocationParser
{
    public static DotNetTestOutputLocation? Parse(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        var match = StackTraceLocationRegex().Match(line);
        if (!match.Success
            || !int.TryParse(
                match.Groups["line"].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var lineNumber)
            || lineNumber < 1)
        {
            return null;
        }

        var filePath = match.Groups["path"].Value.Trim();
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        return new DotNetTestOutputLocation(filePath, lineNumber);
    }

    [GeneratedRegex(@"\sin\s+(?<path>.+?\.cs):line\s+(?<line>\d+)\s*$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex StackTraceLocationRegex();
}
