using Toren.Core.Execution.Models;
using Toren.DotNet.Testing.Models;
using Toren.DotNet.Testing.Parsers;

namespace Toren.App.Testing.ViewModels;

public sealed class TestRunOutputLineViewModel(ProcessOutputChannel channel, string text)
{
    public string Text { get; } = text;

    public bool IsStandardOutput { get; } = channel == ProcessOutputChannel.StandardOutput;

    public bool IsStandardError { get; } = channel == ProcessOutputChannel.StandardError;

    public DotNetTestOutputLocation? Location { get; } = DotNetTestOutputLocationParser.Parse(text);

    public bool CanNavigate => Location is not null;
}
