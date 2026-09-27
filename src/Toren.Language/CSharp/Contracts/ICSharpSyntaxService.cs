using Toren.Language.CSharp.Models;

namespace Toren.Language.CSharp.Contracts;

public interface ICSharpSyntaxService
{
    Task<IReadOnlyList<CSharpDiagnostic>> AnalyzeAsync(
        string sourceText,
        CancellationToken cancellationToken = default);
}
