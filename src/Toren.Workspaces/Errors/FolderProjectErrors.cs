using Toren.Core.Results;

namespace Toren.Workspaces.Errors;

public static class FolderProjectErrors
{
    public static OperationError FolderUnavailable(string folderPath) =>
        OperationError.Create(
            "workspace.folder.projects.unavailable",
            $"Workspace folder is unavailable: {folderPath}");

    public static OperationError DiscoveryFailed(string folderPath, string details) =>
        OperationError.Create(
            "workspace.folder.projects.discovery.failed",
            $"Could not discover projects under '{folderPath}': {details}");
}
