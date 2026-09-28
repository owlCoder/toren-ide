using Toren.Workspaces.Models;

namespace Toren.Workspaces.Contracts;

public interface IWorkspaceFileSearchService
{
    IReadOnlyList<WorkspaceFileEntry> Search(
        IReadOnlyList<WorkspaceFileEntry> files,
        string query,
        int maxResults = 50);
}
