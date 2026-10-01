using Toren.Core.Results;
using Toren.Workspaces.Models;

namespace Toren.Workspaces.Contracts;

public interface IProjectEvaluationProvider
{
    /// <summary>
    /// Evaluates project properties, items and declared references without running targets.
    /// Results follow the order of <paramref name="projectPaths"/>. Fails with the error of the
    /// first project, in that order, that cannot be evaluated.
    /// </summary>
    Task<Result<IReadOnlyList<ProjectEvaluation>>> EvaluateAsync(
        IReadOnlyList<string> projectPaths,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the design-time targets that resolve compiler inputs for evaluated projects and
    /// returns their metadata in the same order. A project whose targets cannot run, for
    /// example before restore, keeps its evaluated metadata with
    /// <see cref="ProjectMetadata.CompilerInputsError"/> set instead of failing the batch.
    /// </summary>
    Task<Result<IReadOnlyList<ProjectMetadata>>> ResolveCompilerInputsAsync(
        IReadOnlyList<WorkspaceProject> projects,
        CancellationToken cancellationToken = default);
}
