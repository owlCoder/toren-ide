using Toren.Language.CSharp.Models;

namespace Toren.Language.CSharp.Contracts;

public interface ICSharpWorkspaceDiagnosticService
{
    Task<IReadOnlyList<CSharpDocumentDiagnostics>> AnalyzeDocumentsAsync(
        CSharpSemanticContext context,
        IReadOnlyList<string> documentPaths,
        CancellationToken cancellationToken = default);
}
