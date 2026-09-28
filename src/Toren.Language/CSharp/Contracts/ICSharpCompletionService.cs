using Toren.Language.CSharp.Models;

namespace Toren.Language.CSharp.Contracts;

public interface ICSharpCompletionService
{
    Task<IReadOnlyList<CSharpCompletionItem>> GetCompletionsAsync(
        CSharpSemanticContext context,
        int line,
        int column,
        CancellationToken cancellationToken = default);
}
