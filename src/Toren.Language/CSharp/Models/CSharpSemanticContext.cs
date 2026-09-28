namespace Toren.Language.CSharp.Models;

public sealed record CSharpSemanticContext(
    string ActiveDocumentPath,
    IReadOnlyList<CSharpSourceDocument> Documents);
