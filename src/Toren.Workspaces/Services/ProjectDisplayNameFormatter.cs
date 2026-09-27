namespace Toren.Workspaces.Services;

internal static class ProjectDisplayNameFormatter
{
    public static IReadOnlyList<string> Format(IReadOnlyList<string> projectPaths)
    {
        ArgumentNullException.ThrowIfNull(projectPaths);

        var names = projectPaths
            .Select(Path.GetFileNameWithoutExtension)
            .ToArray();
        var displayNames = new string[names.Length];

        for (var index = 0; index < names.Length; index++)
        {
            displayNames[index] = FindShortestUniqueSuffix(index, names, projectPaths[index]);
        }

        return displayNames;
    }

    private static string FindShortestUniqueSuffix(int index, string[] names, string projectPath)
    {
        var segments = names[index].Split('.', StringSplitOptions.RemoveEmptyEntries);
        for (var segmentCount = 1; segmentCount <= segments.Length; segmentCount++)
        {
            var candidate = string.Join('.', segments[^segmentCount..]);
            var matchCount = names.Count(name => HasSuffix(name, candidate));
            if (matchCount == 1)
            {
                return candidate;
            }
        }

        var parent = Path.GetFileName(Path.GetDirectoryName(projectPath));
        return string.IsNullOrWhiteSpace(parent)
            ? names[index]
            : $"{parent}/{names[index]}";
    }

    private static bool HasSuffix(string projectName, string candidate) =>
        projectName.Equals(candidate, StringComparison.OrdinalIgnoreCase)
        || projectName.EndsWith($".{candidate}", StringComparison.OrdinalIgnoreCase);
}
