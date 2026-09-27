using Toren.Language.CSharp.Models;

namespace Toren.App.Diagnostics.Contracts;

public interface IDocumentDiagnosticsCoordinator : IDisposable
{
    Task<IReadOnlyList<CSharpDiagnostic>?> AnalyzeLatestAsync(
        string path,
        string sourceText,
        bool debounce,
        CancellationToken cancellationToken = default);

    void CancelPending();
}
