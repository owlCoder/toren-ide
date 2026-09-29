using Toren.Core.Execution.Models;

namespace Toren.App.Testing.ViewModels;

public sealed class TestRunOutputLineViewModel(ProcessOutputChannel channel, string text)
{
    public string Text { get; } = text;

    public bool IsStandardOutput { get; } = channel == ProcessOutputChannel.StandardOutput;

    public bool IsStandardError { get; } = channel == ProcessOutputChannel.StandardError;
}
