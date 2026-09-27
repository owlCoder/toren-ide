using Toren.Core.Results;

namespace Toren.Workspaces.Errors;

public static class SolutionProjectErrors
{
    public static OperationError ListFailed(string details) =>
        OperationError.Create("workspace.solution.list.failed", $"Could not list solution projects: {details}");
}
