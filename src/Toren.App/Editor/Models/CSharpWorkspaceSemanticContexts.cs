using Toren.Core.Results;
using Toren.Language.CSharp.Models;

namespace Toren.App.Editor.Models;

public sealed record CSharpWorkspaceProjectContext(
    CSharpSemanticContext SemanticContext,
    IReadOnlyList<string> DocumentPaths,
    string? ProjectPath = null);

public sealed record CSharpWorkspaceSemanticContexts(
    IReadOnlyList<CSharpWorkspaceProjectContext> ProjectContexts,
    IReadOnlyList<CSharpSourceDocument> LooseDocuments,
    OperationError ProjectSystemError = default);
