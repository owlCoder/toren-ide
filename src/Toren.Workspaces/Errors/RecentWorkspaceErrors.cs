using Toren.Core.Results;

namespace Toren.Workspaces.Errors;

public static class RecentWorkspaceErrors
{
    public static OperationError InvalidFormat() =>
        OperationError.Create("workspace.history.invalid", "Recent workspace history has an invalid or unsupported format.");

    public static OperationError ReadFailed(string details) =>
        OperationError.Create("workspace.history.read.failed", $"Could not load recent workspaces: {details}");

    public static OperationError WriteFailed(string details) =>
        OperationError.Create("workspace.history.write.failed", $"Could not save recent workspaces: {details}");
}
