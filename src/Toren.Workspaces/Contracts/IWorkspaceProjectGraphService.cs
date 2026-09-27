using Toren.Core.Results;
using Toren.Workspaces.Models;

namespace Toren.Workspaces.Contracts;

public interface IWorkspaceProjectGraphService
{
    Task<Result<WorkspaceProjectGraph>> LoadAsync(
        WorkspaceDescriptor workspace,
        CancellationToken cancellationToken = default);
}
