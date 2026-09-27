using Toren.Core.Results;
using Toren.Workspaces.Models;

namespace Toren.Workspaces.Contracts;

public interface IProjectReferenceProvider
{
    Task<Result<IReadOnlyList<ProjectReferenceInfo>>> GetReferencesAsync(
        string projectPath,
        CancellationToken cancellationToken = default);
}
