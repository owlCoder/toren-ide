using Toren.Language.CSharp.Models;

namespace Toren.App.Diagnostics.Contracts;

public interface IDocumentDiagnosticsCoordinator : IDisposable
{
    Task<IReadOnlyList<CSharpDiagnostic>?> AnalyzeLatestAsync(
        CSharpSemanticContext context,
        bool debounce,
        CancellationToken cancellationToken = default);

    void CancelPending();
}
