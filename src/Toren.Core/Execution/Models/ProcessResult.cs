namespace Toren.Core.Execution.Models;

public sealed record ProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError)
{
    public bool Succeeded => ExitCode == 0;
}
