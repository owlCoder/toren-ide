using Toren.Language.CSharp.Models;

namespace Toren.App.Diagnostics.ViewModels;

public sealed class ProblemItemViewModel
{
    public ProblemItemViewModel(string filePath, CSharpDiagnostic diagnostic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(diagnostic);

        FilePath = filePath;
        FileName = Path.GetFileName(filePath) ?? filePath;
        Code = diagnostic.Id;
        Message = diagnostic.Message;
        Severity = diagnostic.Severity;
        StartLine = diagnostic.StartLine;
        StartColumn = diagnostic.StartColumn;
    }

    public string FilePath { get; }

    public string FileName { get; }

    public string Code { get; }

    public string Message { get; }

    public CSharpDiagnosticSeverity Severity { get; }

    public int StartLine { get; }

    public int StartColumn { get; }

    public string Location => $"Ln {StartLine}, Col {StartColumn}";

    public bool IsError => Severity == CSharpDiagnosticSeverity.Error;

    public bool IsWarning => Severity == CSharpDiagnosticSeverity.Warning;

    public bool IsInfo => Severity == CSharpDiagnosticSeverity.Info;
}
