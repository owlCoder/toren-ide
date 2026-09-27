using Toren.Core.Results;
using Toren.Workspaces.Models;

namespace Toren.Workspaces.Contracts;

public interface IProjectMetadataProvider
{
    Task<Result<ProjectMetadata>> GetMetadataAsync(
        string projectPath,
        CancellationToken cancellationToken = default);
}
