using Toren.Core.IO;
using Toren.Workspaces.Models;

namespace Toren.Workspaces.Services;

/// <summary>
/// Answers which project a file belongs to. Paths are normalized and indexed once, so lookups
/// stay cheap when every file of a large workspace is classified.
/// </summary>
public sealed class WorkspaceProjectOwnershipMap
{
    private readonly Dictionary<string, WorkspaceProject> _sourceOwners = new(FileSystemPath.Comparer);
    private readonly Dictionary<string, WorkspaceProject> _directoryOwners = new(FileSystemPath.Comparer);
    private readonly Dictionary<WorkspaceProject, HashSet<string>> _projectSources =
        new(ReferenceEqualityComparer.Instance);

    public WorkspaceProjectOwnershipMap(IReadOnlyList<WorkspaceProject> projects)
    {
        ArgumentNullException.ThrowIfNull(projects);

        // Earlier projects win ties, both for a shared directory and for a linked source file.
        foreach (var project in projects)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(project.Path))
                ?? Directory.GetCurrentDirectory();
            _directoryOwners.TryAdd(directory, project);

            var sources = new HashSet<string>(project.Metadata.SourcePaths.Count, FileSystemPath.Comparer);
            foreach (var source in project.Metadata.SourcePaths)
            {
                var fullPath = Path.GetFullPath(source);
                sources.Add(fullPath);
                _sourceOwners.TryAdd(fullPath, project);
            }

            _projectSources.TryAdd(project, sources);
        }
    }

    public WorkspaceProject? FindOwningProject(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var fullPath = Path.GetFullPath(filePath);
        if (_sourceOwners.TryGetValue(fullPath, out var explicitOwner))
        {
            return explicitOwner;
        }

        // Otherwise the nearest enclosing project directory owns the file.
        for (var directory = fullPath; directory is not null; directory = Path.GetDirectoryName(directory))
        {
            if (_directoryOwners.TryGetValue(directory, out var owner))
            {
                return owner;
            }
        }

        return null;
    }

    /// <summary>Whether the project's evaluated compile items include the file.</summary>
    public bool ListsSource(WorkspaceProject project, string filePath)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return _projectSources.TryGetValue(project, out var sources)
            && sources.Contains(Path.GetFullPath(filePath));
    }
}
