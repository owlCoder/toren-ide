using Toren.Workspaces.Contracts;
using Toren.Workspaces.Models;

namespace Toren.Workspaces.Services;

public sealed class WorkspaceClassifier : IWorkspaceClassifier
{
    public WorkspaceDescriptor ClassifyDirectory(string path)
    {
        var fullPath = NormalizePath(path);
        var trimmedPath = Path.TrimEndingDirectorySeparator(fullPath);
        var displayName = Path.GetFileName(trimmedPath);

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = fullPath;
        }

        return new WorkspaceDescriptor(fullPath, displayName, WorkspaceKind.Folder);
    }

    public bool TryClassifyFile(string path, out WorkspaceDescriptor? descriptor)
    {
        var fullPath = NormalizePath(path);
        var kind = Path.GetExtension(fullPath).ToLowerInvariant() switch
        {
            ".sln" => WorkspaceKind.Solution,
            ".slnx" => WorkspaceKind.SolutionX,
            ".csproj" => WorkspaceKind.Project,
            _ => (WorkspaceKind?)null,
        };

        if (kind is null)
        {
            descriptor = null;
            return false;
        }

        descriptor = new WorkspaceDescriptor(
            fullPath,
            Path.GetFileNameWithoutExtension(fullPath),
            kind.Value);

        return true;
    }

    private static string NormalizePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Path.GetFullPath(path);
    }
}
