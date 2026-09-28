using Toren.Language.CSharp.Models;

namespace Toren.App.Diagnostics.Contracts;

public interface IDocumentDiagnosticsCoordinator : IDisposable
{
    Task<IReadOnlyList<CSharpDiagnostic>?> AnalyzeLatestAsync(
        string sourceText,
        CSharpSemanticContext? semanticContext,
        bool debounce,
        CancellationToken cancellationToken = default);

    void CancelPending();
}
