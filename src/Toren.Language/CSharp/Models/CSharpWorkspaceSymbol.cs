namespace Toren.Language.CSharp.Models;

public sealed record CSharpWorkspaceSymbol(
    string Name,
    string DisplayText,
    CSharpSymbolKind Kind,
    string? ContainerName,
    CSharpSourceLocation Location);
