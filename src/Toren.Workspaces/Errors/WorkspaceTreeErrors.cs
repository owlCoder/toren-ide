using Toren.Core.Results;

namespace Toren.Workspaces.Errors;

public static class WorkspaceTreeErrors
{
    public static OperationError PathUnavailable(string path) =>
        OperationError.Create("workspace.path.unavailable", $"Workspace path is unavailable: {path}");

    public static OperationError ReadFailed(string path, string details) =>
        OperationError.Create("workspace.read.failed", $"Could not read '{path}': {details}");

    public static OperationError SolutionListFailed(string details) =>
        OperationError.Create("workspace.solution.list.failed", $"Could not list solution projects: {details}");

    public static OperationError ProjectEvaluationFailed(string details) =>
        OperationError.Create("workspace.project.evaluate.failed", $"Could not evaluate project references: {details}");
}
