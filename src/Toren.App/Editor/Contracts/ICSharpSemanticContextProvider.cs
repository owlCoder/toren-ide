using Toren.App.ViewModels;
using Toren.Language.CSharp.Models;

namespace Toren.App.Editor.Contracts;

public interface ICSharpSemanticContextProvider
{
    Task<CSharpSemanticContext?> CreateAsync(
        string workspacePath,
        OpenDocumentViewModel activeDocument,
        CancellationToken cancellationToken = default);
}
