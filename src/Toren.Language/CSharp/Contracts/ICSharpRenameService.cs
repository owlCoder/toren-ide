using Toren.Language.CSharp.Models;

namespace Toren.Language.CSharp.Contracts;

public interface ICSharpRenameService
{
    Task<CSharpRenameResult?> RenameAsync(
        CSharpSemanticContext context,
        int line,
        int column,
        string newName,
        CancellationToken cancellationToken = default);
}
