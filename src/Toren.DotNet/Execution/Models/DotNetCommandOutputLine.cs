namespace Toren.DotNet.Execution.Models;

public enum DotNetCommandOutputChannel
{
    StandardOutput,
    StandardError,
}

public sealed record DotNetCommandOutputLine(
    DotNetCommandOutputChannel Channel,
    string Text);
