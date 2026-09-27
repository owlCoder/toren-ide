using Toren.Core.Results;
using Toren.Workspaces.Models;

namespace Toren.Workspaces.Contracts;

public interface IRecentWorkspaceStore
{
    Task<Result<IReadOnlyList<WorkspaceDescriptor>>> LoadAsync(CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<WorkspaceDescriptor>>> RecordAsync(
        WorkspaceDescriptor workspace,
        CancellationToken cancellationToken = default);
}
