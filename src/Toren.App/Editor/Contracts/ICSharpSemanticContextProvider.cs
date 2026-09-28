using Toren.Language.CSharp.Models;

namespace Toren.App.Editor.Contracts;

public interface ICSharpSemanticContextProvider
{
    Task<CSharpSemanticContext?> CreateAsync(
        string workspacePath,
        CSharpSourceDocument activeDocument,
        IReadOnlyList<CSharpSourceDocument> openDocuments,
        CancellationToken cancellationToken = default);
}
