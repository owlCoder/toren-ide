namespace Toren.DotNet.Execution.Models;

public sealed record DotNetCommandResult(
    DotNetCommandKind Kind,
    int ExitCode,
    string StandardOutput,
    string StandardError)
{
    public bool Succeeded => ExitCode == 0;
}
