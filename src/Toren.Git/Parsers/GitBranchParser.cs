using Toren.Git.Models;

namespace Toren.Git.Parsers;

public static class GitBranchParser
{
    public static IReadOnlyList<GitBranchInfo> Parse(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return [];
        }

        var branches = new List<GitBranchInfo>();
        foreach (var rawLine in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.TrimEnd('\r');
            var fields = line.Split('\0');
            if (fields.Length < 3 || string.IsNullOrWhiteSpace(fields[0]))
            {
                continue;
            }

            branches.Add(new GitBranchInfo(
                fields[0],
                string.Equals(fields[1], "*", StringComparison.Ordinal),
                string.IsNullOrWhiteSpace(fields[2]) ? null : fields[2]));
        }

        return branches;
    }
}
