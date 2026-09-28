using Toren.Language.CSharp.Models;

namespace Toren.Language.CSharp.Contracts;

public interface ICSharpSymbolIndexService
{
    Task<IReadOnlyList<CSharpWorkspaceSymbol>> GetSymbolsAsync(
        CSharpSemanticContext context,
        CancellationToken cancellationToken = default);
}
