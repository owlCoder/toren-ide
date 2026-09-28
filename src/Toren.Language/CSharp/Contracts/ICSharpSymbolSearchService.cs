using Toren.Language.CSharp.Models;

namespace Toren.Language.CSharp.Contracts;

public interface ICSharpSymbolSearchService
{
    IReadOnlyList<CSharpWorkspaceSymbol> Search(
        IReadOnlyList<CSharpWorkspaceSymbol> symbols,
        string query,
        int maxResults = 75);
}
