namespace Toren.App.Execution.Models;

public sealed record WorkspaceExecutionTarget(
    string ProjectPath,
    string DisplayName,
    IReadOnlyList<string> TargetFrameworks);
