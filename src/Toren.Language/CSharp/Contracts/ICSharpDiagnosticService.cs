using Toren.Language.CSharp.Models;

namespace Toren.Language.CSharp.Contracts;

public interface ICSharpDiagnosticService
{
    Task<IReadOnlyList<CSharpDiagnostic>> AnalyzeAsync(
        CSharpSemanticContext context,
        CancellationToken cancellationToken = default);
}
