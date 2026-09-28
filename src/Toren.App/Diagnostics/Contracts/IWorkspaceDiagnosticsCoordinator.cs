using Toren.Language.CSharp.Models;

namespace Toren.App.Diagnostics.Contracts;

public interface IWorkspaceDiagnosticsCoordinator : IDisposable
{
    Task<IReadOnlyList<CSharpDocumentDiagnostics>?> AnalyzeLatestAsync(
        string workspacePath,
        IReadOnlyList<CSharpSourceDocument> openDocuments,
        CancellationToken cancellationToken = default);

    void CancelPending();
}
