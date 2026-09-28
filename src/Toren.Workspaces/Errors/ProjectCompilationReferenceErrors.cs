using Toren.Core.Results;

namespace Toren.Workspaces.Errors;

public static class ProjectCompilationReferenceErrors
{
    public static OperationError ResolutionFailed(string details) =>
        OperationError.Create(
            "workspace.project.compilation-references.resolve.failed",
            $"Could not resolve project compilation references: {details}");
}
