using Toren.Workspaces.Models;

namespace Toren.Workspaces.Services;

public sealed class WorkspaceProjectOwnershipMap
{
    private readonly ProjectDirectory[] _projects;

    public WorkspaceProjectOwnershipMap(IReadOnlyList<WorkspaceProject> projects)
    {
        ArgumentNullException.ThrowIfNull(projects);
        _projects = projects
            .Select(project => new ProjectDirectory(
                project,
                Path.GetDirectoryName(Path.GetFullPath(project.Path))
                    ?? Directory.GetCurrentDirectory()))
            .ToArray();
    }

    public WorkspaceProject? FindOwningProject(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var fullPath = Path.GetFullPath(filePath);
        var explicitOwner = _projects.Select(project => project.Project).FirstOrDefault(project =>
            project.Metadata.SourcePaths.Any(source => string.Equals(Path.GetFullPath(source), fullPath,
                OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)));
        if (explicitOwner is not null) return explicitOwner;
        return _projects
            .Where(project => IsWithinDirectory(fullPath, project.Directory))
            .OrderByDescending(project => project.Directory.Length)
            .Select(project => project.Project)
            .FirstOrDefault();
    }

    private static bool IsWithinDirectory(string filePath, string directoryPath)
    {
        var relativePath = Path.GetRelativePath(directoryPath, filePath);
        if (relativePath.Equals("..", StringComparison.Ordinal))
        {
            return false;
        }

        var parentPrefix = $"..{Path.DirectorySeparatorChar}";
        return !relativePath.StartsWith(parentPrefix, StringComparison.Ordinal)
            && !Path.IsPathRooted(relativePath);
    }

    private sealed record ProjectDirectory(WorkspaceProject Project, string Directory);
}
