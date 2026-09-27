using Toren.Core.Results;
using Toren.Workspaces.Models;

namespace Toren.Workspaces.Contracts;

public interface IWorkspaceTreeService
{
    Result<WorkspaceNode> CreateRoot(WorkspaceDescriptor workspace);

    Task<Result<IReadOnlyList<WorkspaceNode>>> GetChildrenAsync(
        WorkspaceNode node,
        CancellationToken cancellationToken = default);
}
