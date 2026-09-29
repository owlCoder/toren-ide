using Toren.DotNet.Testing.Models;

namespace Toren.DotNet.Testing.Services;

public static class DotNetTestListParser
{
    private static readonly string[] SectionMarkers =
    [
        "The following Tests are available:",
        "The following tests are available:",
    ];

    public static IReadOnlyList<DotNetTestCase> Parse(string output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var tests = new List<DotNetTestCase>();
        var inList = false;
        foreach (var rawLine in output.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var trimmed = rawLine.Trim();
            if (!inList)
            {
                inList = SectionMarkers.Any(marker => trimmed.Equals(marker, StringComparison.OrdinalIgnoreCase));
                continue;
            }

            if (trimmed.Length == 0)
            {
                continue;
            }

            if (!char.IsWhiteSpace(rawLine, 0))
            {
                break;
            }

            tests.Add(new DotNetTestCase(trimmed, GetDisplayName(trimmed)));
        }

        return tests;
    }

    private static string GetDisplayName(string fullyQualifiedName)
    {
        var parameterIndex = fullyQualifiedName.IndexOf('(', StringComparison.Ordinal);
        var searchLength = parameterIndex >= 0 ? parameterIndex : fullyQualifiedName.Length;
        var separatorIndex = fullyQualifiedName.LastIndexOf('.', searchLength - 1, searchLength);
        return separatorIndex >= 0
            ? fullyQualifiedName[(separatorIndex + 1)..]
            : fullyQualifiedName;
    }
}
