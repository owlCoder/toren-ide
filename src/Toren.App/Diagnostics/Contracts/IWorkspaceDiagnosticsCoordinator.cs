using Toren.App.Diagnostics.Models;
using Toren.Language.CSharp.Models;

namespace Toren.App.Diagnostics.Contracts;

public interface IWorkspaceDiagnosticsCoordinator : IDisposable
{
    Task<WorkspaceDiagnosticsSnapshot?> AnalyzeLatestAsync(
        string workspacePath,
        IReadOnlyList<CSharpSourceDocument> openDocuments,
        CancellationToken cancellationToken = default);

    void CancelPending();
}
