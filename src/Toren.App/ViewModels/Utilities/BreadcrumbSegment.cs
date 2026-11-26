namespace Toren.App.ViewModels.Utilities;

/// <summary>One clickable part of the breadcrumb path above an editor.</summary>
public sealed record BreadcrumbSegment(string Name, string Path, bool HasNext);

internal static class PathComparisons
{
    public static StringComparison Current { get; } =
        OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    public static bool IsInside(string directory, string path)
    {
        var prefix = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, Current);
    }

    public static bool AreSame(string left, string right) =>
        string.Equals(
            left.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            right.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Current);
}

internal static class BreadcrumbBuilder
{
    private static readonly char[] Separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    /// <summary>
    /// Builds "workspace › folder › file" for a document inside the workspace, and
    /// "parent folder › file" for a document outside of it.
    /// </summary>
    public static IReadOnlyList<BreadcrumbSegment> Build(string documentPath, string? workspaceRoot)
    {
        var fullPath = Path.GetFullPath(documentPath);
        var parts = new List<(string Name, string Path)>();

        if (!string.IsNullOrWhiteSpace(workspaceRoot)
            && PathComparisons.IsInside(Path.GetFullPath(workspaceRoot), fullPath))
        {
            var root = Path.GetFullPath(workspaceRoot).TrimEnd(Separators);
            var rootName = Path.GetFileName(root);
            parts.Add((rootName.Length > 0 ? rootName : root, root));

            var current = root;
            foreach (var part in Path.GetRelativePath(root, fullPath)
                         .Split(Separators, StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, part);
                parts.Add((part, current));
            }
        }
        else
        {
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                var directoryName = Path.GetFileName(directory);
                parts.Add((directoryName.Length > 0 ? directoryName : directory, directory));
            }

            parts.Add((Path.GetFileName(fullPath), fullPath));
        }

        var segments = new BreadcrumbSegment[parts.Count];
        for (var index = 0; index < parts.Count; index++)
        {
            segments[index] = new BreadcrumbSegment(parts[index].Name, parts[index].Path, index < parts.Count - 1);
        }

        return segments;
    }
}
