using Toren.Core.Results;

namespace Toren.Workspaces.Errors;

public static class WorkspaceTreeErrors
{
    public static OperationError PathUnavailable(string path) =>
        OperationError.Create("workspace.path.unavailable", $"Workspace path is unavailable: {path}");

    public static OperationError ReadFailed(string path, string details) =>
        OperationError.Create("workspace.read.failed", $"Could not read '{path}': {details}");
}
