using Toren.Core.Results;

namespace Toren.Workspaces.Errors;

public static class ProjectMetadataErrors
{
    public static OperationError EvaluationFailed(string details) =>
        OperationError.Create(
            "workspace.project.metadata.evaluate.failed",
            $"Could not evaluate project metadata: {details}");
}
