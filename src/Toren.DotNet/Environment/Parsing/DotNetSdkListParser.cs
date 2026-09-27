using Toren.DotNet.Environment.Models;

namespace Toren.DotNet.Environment.Parsing;

public static class DotNetSdkListParser
{
    public static IReadOnlyList<DotNetSdkInfo> Parse(string output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var sdks = new List<DotNetSdkInfo>();
        using var reader = new StringReader(output);

        while (reader.ReadLine() is { } line)
        {
            var openingBracketIndex = line.IndexOf('[', StringComparison.Ordinal);
            var closingBracketIndex = line.LastIndexOf(']');

            if (openingBracketIndex <= 0 || closingBracketIndex <= openingBracketIndex)
            {
                continue;
            }

            var version = line[..openingBracketIndex].Trim();
            var basePath = line[(openingBracketIndex + 1)..closingBracketIndex].Trim();

            if (version.Length == 0 || basePath.Length == 0)
            {
                continue;
            }

            sdks.Add(new DotNetSdkInfo(version, basePath));
        }

        return sdks;
    }
}
