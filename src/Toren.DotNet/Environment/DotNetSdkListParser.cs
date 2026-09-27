namespace Toren.DotNet.Environment;

public static class DotNetSdkListParser
{
    public static IReadOnlyList<DotNetSdkInfo> Parse(string output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var sdks = new List<DotNetSdkInfo>();
        using var reader = new StringReader(output);

        while (reader.ReadLine() is { } line)
        {
            var separatorIndex = line.LastIndexOf(" [", StringComparison.Ordinal);
            if (separatorIndex <= 0 || !line.EndsWith(']'))
            {
                continue;
            }

            var version = line[..separatorIndex].Trim();
            var basePath = line[(separatorIndex + 2)..^1].Trim();

            if (version.Length == 0 || basePath.Length == 0)
            {
                continue;
            }

            sdks.Add(new DotNetSdkInfo(version, basePath));
        }

        return sdks;
    }
}
