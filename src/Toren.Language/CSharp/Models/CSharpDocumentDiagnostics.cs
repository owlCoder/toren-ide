namespace Toren.Language.CSharp.Models;

public sealed record CSharpDocumentDiagnostics(
    string FilePath,
    IReadOnlyList<CSharpDiagnostic> Diagnostics);
