using Toren.App.Diagnostics.Models;
using Toren.Language.CSharp.Models;

namespace Toren.App.Diagnostics.ViewModels;

public sealed class ProblemItemViewModel
{
    public ProblemItemViewModel(string filePath, CSharpDiagnostic diagnostic)
        : this(new ProblemDiagnostic(
            diagnostic.Id,
            diagnostic.Message,
            diagnostic.Severity switch
            {
                CSharpDiagnosticSeverity.Error => ProblemSeverity.Error,
                CSharpDiagnosticSeverity.Warning => ProblemSeverity.Warning,
                CSharpDiagnosticSeverity.Info => ProblemSeverity.Info,
                _ => throw new InvalidOperationException($"Unsupported C# diagnostic severity: {diagnostic.Severity}."),
            },
            "C#",
            filePath,
            StartLine: diagnostic.StartLine,
            StartColumn: diagnostic.StartColumn))
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(diagnostic);
    }

    public ProblemItemViewModel(ProblemDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        ArgumentException.ThrowIfNullOrWhiteSpace(diagnostic.Code);
        ArgumentException.ThrowIfNullOrWhiteSpace(diagnostic.Message);
        ArgumentException.ThrowIfNullOrWhiteSpace(diagnostic.Source);

        FilePath = string.IsNullOrWhiteSpace(diagnostic.FilePath)
            ? string.Empty
            : Path.GetFullPath(diagnostic.FilePath);
        ProjectPath = string.IsNullOrWhiteSpace(diagnostic.ProjectPath)
            ? null
            : Path.GetFullPath(diagnostic.ProjectPath);
        FileName = string.IsNullOrEmpty(FilePath)
            ? diagnostic.Source
            : Path.GetFileName(FilePath) is { Length: > 0 } fileName
                ? fileName
                : FilePath;
        Code = diagnostic.Code;
        Message = diagnostic.Message;
        Severity = diagnostic.Severity;
        StartLine = diagnostic.StartLine;
        StartColumn = diagnostic.StartColumn;
    }

    public string FilePath { get; }

    public string? ProjectPath { get; }

    public string FileName { get; }

    public string Code { get; }

    public string Message { get; }

    public ProblemSeverity Severity { get; }

    public int StartLine { get; }

    public int StartColumn { get; }

    public bool CanNavigate => !string.IsNullOrEmpty(FilePath) && StartLine > 0 && StartColumn > 0;

    public string Location => CanNavigate ? $"Ln {StartLine}, Col {StartColumn}" : string.Empty;

    public bool IsError => Severity == ProblemSeverity.Error;

    public bool IsWarning => Severity == ProblemSeverity.Warning;

    public bool IsInfo => Severity == ProblemSeverity.Info;
}
