namespace Toren.Language.CSharp.Models;

public sealed record CSharpSymbolInfo(
    string Name,
    string DisplayText,
    CSharpSymbolKind Kind,
    CSharpSourceLocation? Definition);
