using Toren.Core.Results;
using Toren.Workspaces.Models;

namespace Toren.Workspaces.Contracts;

public interface IWorkspaceProjectCatalog
{
    /// <summary>
    /// Returns the evaluated projects of a workspace in workspace order. Design-time compiler
    /// inputs are not resolved; consumers that compile sources use
    /// <see cref="IWorkspaceProjectGraphService"/> instead.
    /// </summary>
    Task<Result<IReadOnlyList<WorkspaceProject>>> GetProjectsAsync(
        WorkspaceDescriptor workspace,
        CancellationToken cancellationToken = default);
}
