using Toren.Core.Results;
using Toren.Workspaces.Models;

namespace Toren.Workspaces.Contracts;

public interface IWorkspaceProjectGraphService
{
    /// <summary>
    /// Returns the projects of a workspace with their design-time compiler inputs: sources,
    /// analyzers, global usings and reference assemblies. Consumers that only need the list of
    /// projects use <see cref="IWorkspaceProjectCatalog"/>, which does not wait for those.
    /// </summary>
    Task<Result<WorkspaceProjectGraph>> LoadAsync(
        WorkspaceDescriptor workspace,
        CancellationToken cancellationToken = default);
}
