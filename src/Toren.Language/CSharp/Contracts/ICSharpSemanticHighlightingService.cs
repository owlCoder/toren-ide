using Toren.Language.CSharp.Models;

namespace Toren.Language.CSharp.Contracts;

public interface ICSharpSemanticHighlightingService
{
    Task<IReadOnlyList<CSharpSemanticHighlight>> GetHighlightsAsync(
        CSharpSemanticContext context,
        CancellationToken cancellationToken = default);
}
