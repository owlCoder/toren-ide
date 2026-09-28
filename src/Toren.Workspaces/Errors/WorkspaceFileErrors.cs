using Toren.Core.Results;

namespace Toren.Workspaces.Errors;

public static class WorkspaceFileErrors
{
    public static OperationError WorkspaceUnavailable(string path) =>
        OperationError.Create(
            "workspace.files.unavailable",
            $"Workspace path is unavailable: {path}");

    public static OperationError DiscoveryFailed(string path, string details) =>
        OperationError.Create(
            "workspace.files.discovery.failed",
            $"Could not discover files under '{path}': {details}");
}
