namespace Toren.DotNet.Execution.Models;

public sealed record DotNetCommandRequest(
    DotNetCommandKind Kind,
    string WorkingDirectory,
    string? TargetPath = null,
    string? Configuration = null,
    string? TargetFramework = null,
    bool NoRestore = false);
