using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;

namespace Toren.App.Terminal.ViewModels;

public sealed partial class TerminalViewModel(
    IInteractiveProcessRunner processRunner,
    INativeShellProvider shellProvider) : ObservableObject, IAsyncDisposable
{
    private readonly IInteractiveProcessRunner _processRunner = processRunner
        ?? throw new ArgumentNullException(nameof(processRunner));
    private readonly INativeShellProvider _shellProvider = shellProvider
        ?? throw new ArgumentNullException(nameof(shellProvider));
    private IInteractiveProcessSession? _session;
    private bool _disposed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmit))]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private string _workingDirectory = Directory.GetCurrentDirectory();

    [ObservableProperty]
    private string _statusText = "Terminal session is stopped.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    [NotifyPropertyChangedFor(nameof(CanStop))]
    [NotifyPropertyChangedFor(nameof(CanSubmit))]
    private bool _isRunning;

    public ObservableCollection<TerminalLineViewModel> Lines { get; } = new();

    public bool CanStart => !IsRunning;

    public bool CanStop => IsRunning;

    public bool CanSubmit => IsRunning && !string.IsNullOrWhiteSpace(InputText);

    public bool HasOutput => Lines.Count > 0;

    public void SetWorkingDirectory(string workingDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        if (IsRunning)
        {
            return;
        }

        WorkingDirectory = Path.GetFullPath(workingDirectory);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || IsRunning)
        {
            return;
        }

        var workingDirectory = Directory.Exists(WorkingDirectory)
            ? WorkingDirectory
            : Directory.GetCurrentDirectory();
        var request = _shellProvider.CreateShellRequest(workingDirectory);
        var synchronizationContext = SynchronizationContext.Current;
        var started = await _processRunner
            .StartAsync(
                request,
                line => ReportOutput(line, synchronizationContext),
                cancellationToken)
            .ConfigureAwait(true);
        if (started.IsFailure)
        {
            StatusText = started.Error.Message;
            return;
        }

        var session = started.Value!;
        _session = session;
        IsRunning = true;
        StatusText = $"Terminal running in {workingDirectory}.";
        AddLine($"[{Path.GetFileName(request.FileName)}] {workingDirectory}");
        _ = ObserveCompletionAsync(session);
    }

    public async Task SubmitAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || !CanSubmit || _session is null)
        {
            return;
        }

        var command = InputText;
        InputText = string.Empty;
        AddLine($"> {command}", isCommand: true);
        var written = await _session.WriteLineAsync(command, cancellationToken).ConfigureAwait(true);
        if (written.IsFailure)
        {
            StatusText = written.Error.Message;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_session is null)
        {
            return;
        }

        var session = _session;
        var stopped = await session.TerminateAsync(cancellationToken).ConfigureAwait(true);
        if (stopped.IsFailure)
        {
            StatusText = stopped.Error.Message;
            return;
        }

        if (ReferenceEquals(_session, session))
        {
            _session = null;
            IsRunning = false;
            StatusText = "Terminal session stopped.";
        }

        await session.DisposeAsync().ConfigureAwait(true);
    }

    public void Clear()
    {
        Lines.Clear();
        OnPropertyChanged(nameof(HasOutput));
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var session = _session;
        _session = null;
        IsRunning = false;
        if (session is not null)
        {
            await session.DisposeAsync().ConfigureAwait(true);
        }
    }

    private async Task ObserveCompletionAsync(IInteractiveProcessSession session)
    {
        var completion = await session.Completion.ConfigureAwait(true);
        if (!ReferenceEquals(_session, session))
        {
            return;
        }

        _session = null;
        IsRunning = false;
        StatusText = completion.IsSuccess
            ? $"Terminal exited with code {completion.Value!.ExitCode}."
            : completion.Error.Message;
        await session.DisposeAsync().ConfigureAwait(true);
    }

    private void ReportOutput(
        ProcessOutputLine line,
        SynchronizationContext? synchronizationContext)
    {
        var isError = line.Channel == ProcessOutputChannel.StandardError;
        if (synchronizationContext is null
            || ReferenceEquals(SynchronizationContext.Current, synchronizationContext))
        {
            AddLine(line.Text, isError);
            return;
        }

        synchronizationContext.Post(
            static state =>
            {
                var report = (OutputReport)state!;
                report.ViewModel.AddLine(report.Text, report.IsError);
            },
            new OutputReport(this, line.Text, isError));
    }

    private void AddLine(string text, bool isError = false, bool isCommand = false)
    {
        Lines.Add(new TerminalLineViewModel(text, isError, isCommand));
        OnPropertyChanged(nameof(HasOutput));
    }

    private sealed record OutputReport(TerminalViewModel ViewModel, string Text, bool IsError);
}
