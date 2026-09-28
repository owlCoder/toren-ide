namespace Toren.Language.CSharp.Models;

public sealed record CSharpCodeActionInfo(
    string Id,
    string Title,
    string DiagnosticId,
    CSharpTextEdit Edit);
