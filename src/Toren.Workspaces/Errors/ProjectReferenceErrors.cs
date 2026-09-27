using Toren.Core.Results;

namespace Toren.Workspaces.Errors;

public static class ProjectReferenceErrors
{
    public static OperationError EvaluationFailed(string details) =>
        OperationError.Create("workspace.project.evaluate.failed", $"Could not evaluate project references: {details}");
}
