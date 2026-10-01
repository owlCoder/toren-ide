using Toren.Core.Results;

namespace Toren.Workspaces.Errors;

public static class WorkspaceProjectGraphErrors
{
    public static OperationError EvaluationAbandoned { get; } =
        OperationError.Create(
            "workspace.project-graph.evaluation.abandoned",
            "Project evaluation stopped because the workspace was closed.");
}
