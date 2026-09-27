using Toren.Core.Results;

namespace Toren.Workspaces.Contracts;

public interface ISolutionProjectProvider
{
    Task<Result<IReadOnlyList<string>>> GetProjectPathsAsync(
        string solutionPath,
        CancellationToken cancellationToken = default);
}
