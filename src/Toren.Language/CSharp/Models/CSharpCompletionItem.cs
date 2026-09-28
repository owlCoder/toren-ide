namespace Toren.Language.CSharp.Models;

public sealed record CSharpCompletionItem(
    string DisplayText,
    string InsertText,
    CSharpSymbolKind Kind,
    string? Detail = null);
