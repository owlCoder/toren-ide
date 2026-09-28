using Toren.Language.CSharp.Models;

namespace Toren.Language.CSharp.Contracts;

public interface ICSharpCodeActionService
{
    Task<IReadOnlyList<CSharpCodeActionInfo>> GetActionsAsync(
        CSharpSemanticContext context,
        int position,
        CancellationToken cancellationToken = default);
}
