using Toren.App.Execution.Models;
using Toren.Core.Results;
using Toren.Workspaces.Models;

namespace Toren.App.Execution.Contracts;

public interface IWorkspaceExecutionTargetService
{
    Task<Result<IReadOnlyList<WorkspaceExecutionTarget>>> GetTargetsAsync(
        WorkspaceDescriptor workspace,
        CancellationToken cancellationToken = default);
}
