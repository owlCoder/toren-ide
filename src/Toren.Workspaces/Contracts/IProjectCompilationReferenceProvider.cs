using Toren.Core.Results;

namespace Toren.Workspaces.Contracts;

public interface IProjectCompilationReferenceProvider
{
    Task<Result<IReadOnlyList<string>>> GetReferencePathsAsync(
        string projectPath,
        string? targetFramework = null,
        CancellationToken cancellationToken = default);
}
