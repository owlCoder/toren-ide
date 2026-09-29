namespace Toren.App.Execution.ViewModels;

public sealed class ExecutionOutputLineViewModel(string text, ExecutionOutputLineKind kind)
{
    public string Text { get; } = text ?? throw new ArgumentNullException(nameof(text));

    public ExecutionOutputLineKind Kind { get; } = kind;

    public bool IsCommand => Kind == ExecutionOutputLineKind.Command;

    public bool IsStandardOutput => Kind == ExecutionOutputLineKind.StandardOutput;

    public bool IsStandardError => Kind == ExecutionOutputLineKind.StandardError;

    public bool IsStatus => Kind == ExecutionOutputLineKind.Status;
}
