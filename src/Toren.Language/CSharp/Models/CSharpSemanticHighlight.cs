namespace Toren.Language.CSharp.Models;

public sealed record CSharpSemanticHighlight(
    int StartOffset,
    int Length,
    CSharpSymbolKind Kind);
