using Toren.Core.Results;

namespace Toren.Workspaces.Contracts;

public interface IFolderProjectProvider
{
    Task<Result<IReadOnlyList<string>>> GetProjectPathsAsync(
        string folderPath,
        CancellationToken cancellationToken = default);
}
