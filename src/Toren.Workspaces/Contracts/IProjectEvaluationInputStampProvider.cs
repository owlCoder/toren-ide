using Toren.Workspaces.Models;

namespace Toren.Workspaces.Contracts;

/// <summary>
/// Describes the current state of the files that project evaluation reads, so an evaluated
/// snapshot is reused only while those files are unchanged.
/// </summary>
public interface IProjectEvaluationInputStampProvider
{
    /// <summary>
    /// Returns a value that changes when a file is added to or removed from the workspace, or
    /// when a solution, project or MSBuild import inside it is modified.
    /// </summary>
    Task<string> GetWorkspaceStampAsync(
        WorkspaceDescriptor workspace,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the state of inputs that are only known after evaluation, such as NuGet assets
    /// files and imports outside the workspace directory.
    /// </summary>
    Task<ProjectInputStamp> GetProjectStampAsync(
        IReadOnlyList<WorkspaceProject> projects,
        CancellationToken cancellationToken = default);
}
