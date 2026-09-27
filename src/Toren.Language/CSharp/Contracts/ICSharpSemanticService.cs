using Toren.Language.CSharp.Models;

namespace Toren.Language.CSharp.Contracts;

public interface ICSharpSemanticService
{
    Task<CSharpSymbolInfo?> GetSymbolAsync(
        string sourceText,
        int line,
        int column,
        CancellationToken cancellationToken = default);
}
