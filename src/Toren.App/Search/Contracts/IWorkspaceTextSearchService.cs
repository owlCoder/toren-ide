using Toren.App.Search.Models;
using Toren.Core.Results;

namespace Toren.App.Search.Contracts;

public interface IWorkspaceTextSearchService
{
    Task<Result<IReadOnlyList<WorkspaceTextSearchResult>>> SearchAsync(
        string workspacePath,
        string query,
        WorkspaceTextSearchOptions options,
        IReadOnlyDictionary<string, string>? textOverrides = null,
        int maxResults = 200,
        CancellationToken cancellationToken = default);
}
