using Toren.App.Diagnostics.Models;
using Toren.Core.Results;

namespace Toren.App.Diagnostics.Contracts;

public interface IProblemsWorkspaceScopeService
{
    Task<Result<ProblemsWorkspaceScopeIndex>> BuildAsync(
        string workspacePath,
        CancellationToken cancellationToken = default);
}
