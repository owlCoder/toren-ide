using Toren.Core.Results;
using Toren.Workspaces.Models;

namespace Toren.Workspaces.Contracts;

public interface IWorkspaceFileProvider
{
    Task<Result<IReadOnlyList<WorkspaceFileEntry>>> GetFilesAsync(
        string workspacePath,
        CancellationToken cancellationToken = default);
}
