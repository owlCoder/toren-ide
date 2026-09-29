namespace Toren.Core.Execution.Models;

public sealed record ProcessOutputLine(ProcessOutputStream Stream, string Text);
