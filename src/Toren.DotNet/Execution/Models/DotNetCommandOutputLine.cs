namespace Toren.DotNet.Execution.Models;

public enum DotNetCommandOutputStream
{
    StandardOutput,
    StandardError,
}

public sealed record DotNetCommandOutputLine(
    DotNetCommandOutputStream Stream,
    string Text);
