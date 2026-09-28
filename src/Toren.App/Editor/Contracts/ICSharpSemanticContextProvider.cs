using Toren.App.Editor.Models;
using Toren.Language.CSharp.Models;

namespace Toren.App.Editor.Contracts;

public interface ICSharpSemanticContextProvider
{
    Task<CSharpSemanticContext?> CreateAsync(
        string workspacePath,
        CSharpSourceDocument activeDocument,
        IReadOnlyList<CSharpSourceDocument> openDocuments,
        CancellationToken cancellationToken = default);

    Task<CSharpSemanticContext?> CreateWorkspaceAsync(
        string workspacePath,
        IReadOnlyList<CSharpSourceDocument> openDocuments,
        CancellationToken cancellationToken = default);

    Task<CSharpWorkspaceSemanticContexts?> CreateWorkspaceProjectContextsAsync(
        string workspacePath,
        IReadOnlyList<CSharpSourceDocument> openDocuments,
        CancellationToken cancellationToken = default);
}
