using Toren.Git.Models;

namespace Toren.Git.Parsers;

public static class GitStatusParser
{
    public static GitRepositoryStatus Parse(string output)
    {
        ArgumentNullException.ThrowIfNull(output);

        string? branchName = null;
        string? upstreamName = null;
        var aheadCount = 0;
        var behindCount = 0;
        var changes = new List<GitChange>();
        var records = output.Split('\0', StringSplitOptions.RemoveEmptyEntries);

        for (var index = 0; index < records.Length; index++)
        {
            var record = records[index];
            if (record.StartsWith("# branch.head ", StringComparison.Ordinal))
            {
                branchName = NormalizeBranchValue(record[14..]);
                continue;
            }

            if (record.StartsWith("# branch.upstream ", StringComparison.Ordinal))
            {
                upstreamName = NormalizeBranchValue(record[18..]);
                continue;
            }

            if (record.StartsWith("# branch.ab ", StringComparison.Ordinal))
            {
                ParseAheadBehind(record[12..], out aheadCount, out behindCount);
                continue;
            }

            if (record.StartsWith("1 ", StringComparison.Ordinal))
            {
                var fields = record.Split(' ', 9, StringSplitOptions.None);
                if (fields.Length == 9)
                {
                    changes.Add(CreateTrackedChange(fields[1], fields[8], originalPath: null));
                }

                continue;
            }

            if (record.StartsWith("2 ", StringComparison.Ordinal))
            {
                var fields = record.Split(' ', 10, StringSplitOptions.None);
                if (fields.Length == 10)
                {
                    var originalPath = index + 1 < records.Length ? records[++index] : null;
                    changes.Add(CreateTrackedChange(fields[1], fields[9], originalPath));
                }

                continue;
            }

            if (record.StartsWith("? ", StringComparison.Ordinal))
            {
                changes.Add(new GitChange(record[2..], null, '?', '?', IsUntracked: true));
            }
        }

        return new GitRepositoryStatus(
            branchName,
            upstreamName,
            aheadCount,
            behindCount,
            changes);
    }

    private static GitChange CreateTrackedChange(string status, string path, string? originalPath)
    {
        var indexStatus = status.Length > 0 ? status[0] : '.';
        var workTreeStatus = status.Length > 1 ? status[1] : '.';
        return new GitChange(path, originalPath, indexStatus, workTreeStatus);
    }

    private static string? NormalizeBranchValue(string value)
    {
        var trimmed = value.Trim();
        return trimmed is "(detached)" or "(unknown)" ? null : trimmed;
    }

    private static void ParseAheadBehind(string value, out int ahead, out int behind)
    {
        ahead = 0;
        behind = 0;
        foreach (var token in value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Length < 2 || !int.TryParse(token.AsSpan(1), out var count))
            {
                continue;
            }

            if (token[0] == '+')
            {
                ahead = count;
            }
            else if (token[0] == '-')
            {
                behind = count;
            }
        }
    }
}
