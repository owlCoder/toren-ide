using Toren.App.Testing.Models;
using Toren.Core.Results;
using Toren.Workspaces.Models;

namespace Toren.App.Testing.Contracts;

public interface IWorkspaceTestDiscoveryService
{
    Task<Result<IReadOnlyList<WorkspaceTestProjectDiscovery>>> DiscoverAsync(
        WorkspaceDescriptor workspace,
        CancellationToken cancellationToken = default);
}
