namespace Toren.App.Diagnostics.Models;

public enum ProblemSeverity
{
    Info,
    Warning,
    Error,
}

public sealed record ProblemDiagnostic(
    string Code,
    string Message,
    ProblemSeverity Severity,
    string Source,
    string? FilePath = null,
    string? ProjectPath = null,
    int StartLine = 0,
    int StartColumn = 0);
