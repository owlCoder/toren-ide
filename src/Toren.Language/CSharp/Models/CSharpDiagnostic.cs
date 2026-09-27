namespace Toren.Language.CSharp.Models;

public sealed record CSharpDiagnostic(
    string Id,
    string Message,
    CSharpDiagnosticSeverity Severity,
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn);
