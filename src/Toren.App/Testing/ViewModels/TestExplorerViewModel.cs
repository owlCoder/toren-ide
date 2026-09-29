using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Toren.App.Testing.Models;
using Toren.Core.Execution.Models;
using Toren.DotNet.Testing.Contracts;
using Toren.DotNet.Testing.Models;

namespace Toren.App.Testing.ViewModels;

public sealed partial class TestExplorerViewModel(
    IDotNetTestRunService testRunService,
    IDotNetTestDebugService? testDebugService = null) : ObservableObject, IDisposable
{
    private readonly IDotNetTestRunService _testRunService = testRunService
        ?? throw new ArgumentNullException(nameof(testRunService));
    private readonly IDotNetTestDebugService? _testDebugService = testDebugService;
    private readonly List<WorkspaceTestProjectDiscovery> _failedProjects = [];
    private CancellationTokenSource? _runCancellation;
    private IDotNetTestDebugSession? _debugSession;
    private bool _disposed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(CanDebug))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRunAll))]
    [NotifyPropertyChangedFor(nameof(CanStop))]
    [NotifyPropertyChangedFor(nameof(CanRerunFailedProjects))]
    [NotifyPropertyChangedFor(nameof(CanDebug))]
    private bool _isRunning;

    [ObservableProperty]
    private string _statusText = "Open a workspace to discover tests.";

    public ObservableCollection<WorkspaceTestProjectDiscovery> Projects { get; } = new();

    public ObservableCollection<TestRunOutputLineViewModel> OutputLines { get; } = new();

    public bool HasProjects => Projects.Count > 0;

    public bool IsEmpty => !IsLoading && !HasProjects;

    public int TotalTests => Projects.Sum(static project => project.Tests.Count);

    public bool CanRunAll => HasProjects && !IsLoading && !IsRunning;

    public bool CanStop => IsRunning;

    public bool CanDebug => _testDebugService is not null && !IsLoading && !IsRunning;

    public bool HasOutput => OutputLines.Count > 0;

    public bool HasFailedProjects => _failedProjects.Count > 0;

    public bool CanRerunFailedProjects => HasFailedProjects && !IsLoading && !IsRunning;

    public void BeginRefresh()
    {
        IsLoading = true;
        StatusText = "Discovering tests…";
        OnPropertyChanged(nameof(CanRunAll));
        OnPropertyChanged(nameof(CanRerunFailedProjects));
    }

    public void Replace(IReadOnlyList<WorkspaceTestProjectDiscovery> projects)
    {
        ArgumentNullException.ThrowIfNull(projects);
        Projects.Clear();
        foreach (var project in projects)
        {
            Projects.Add(project);
        }

        ClearFailedProjects();
        IsLoading = false;
        StatusText = Projects.Count == 0
            ? "No test projects found."
            : $"Discovered {TotalTests} test{(TotalTests == 1 ? string.Empty : "s")} in {Projects.Count} project{(Projects.Count == 1 ? string.Empty : "s")}.";
        NotifyCollectionSummary();
    }

    public void SetError(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Projects.Clear();
        ClearFailedProjects();
        IsLoading = false;
        StatusText = message;
        NotifyCollectionSummary();
    }

    public void Reset()
    {
        Stop();
        Projects.Clear();
        ClearFailedProjects();
        OutputLines.Clear();
        IsLoading = false;
        StatusText = "Open a workspace to discover tests.";
        NotifyCollectionSummary();
        OnPropertyChanged(nameof(HasOutput));
    }

    public async Task RunAllAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || !CanRunAll)
        {
            return;
        }

        ClearFailedProjects();
        await RunProjectsAsync(Projects.ToArray(), false, cancellationToken).ConfigureAwait(true);
    }

    public async Task RerunFailedProjectsAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || !CanRerunFailedProjects)
        {
            return;
        }

        var failedProjects = _failedProjects.ToArray();
        ClearFailedProjects();
        await RunProjectsAsync(failedProjects, true, cancellationToken).ConfigureAwait(true);
    }

    public async Task RunTestAsync(
        WorkspaceTestProjectDiscovery project,
        DotNetTestCase test,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(test);
        if (_disposed || IsLoading || IsRunning)
        {
            return;
        }

        using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _runCancellation = runCancellation;
        IsRunning = true;
        OutputLines.Clear();
        OnPropertyChanged(nameof(HasOutput));
        var synchronizationContext = SynchronizationContext.Current;

        try
        {
            StatusText = $"Running {test.DisplayName}…";
            AddOutput(ProcessOutputChannel.StandardOutput, $"[Test] {test.FullyQualifiedName}");
            var result = await _testRunService
                .RunAsync(
                    new DotNetTestRunRequest(
                        project.ProjectPath,
                        FullyQualifiedName: test.FullyQualifiedName,
                        RunnerId: test.RunnerId),
                    line => ReportOutput(line, synchronizationContext),
                    runCancellation.Token)
                .ConfigureAwait(true);
            if (!result.IsSuccess)
            {
                AddOutput(ProcessOutputChannel.StandardError, result.Error.Message);
                StatusText = result.Error.Message;
            }
            else
            {
                StatusText = result.Value.Succeeded
                    ? $"{test.DisplayName} passed."
                    : $"{test.DisplayName} failed.";
            }
        }
        catch (OperationCanceledException) when (runCancellation.IsCancellationRequested)
        {
            StatusText = "Test run canceled.";
        }
        finally
        {
            CompleteRun(runCancellation);
        }
    }

    public async Task DebugTestAsync(
        WorkspaceTestProjectDiscovery project,
        DotNetTestCase test,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(test);
        if (_disposed || !CanDebug || _testDebugService is null)
        {
            return;
        }

        using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _runCancellation = runCancellation;
        IsRunning = true;
        OutputLines.Clear();
        OnPropertyChanged(nameof(HasOutput));
        var synchronizationContext = SynchronizationContext.Current;
        IDotNetTestDebugSession? debugSession = null;

        try
        {
            StatusText = $"Starting debug session for {test.DisplayName}…";
            AddOutput(ProcessOutputChannel.StandardOutput, $"[Debug Test] {test.FullyQualifiedName}");
            var startResult = await _testDebugService
                .StartAsync(
                    new DotNetTestRunRequest(
                        project.ProjectPath,
                        FullyQualifiedName: test.FullyQualifiedName,
                        RunnerId: test.RunnerId),
                    line => ReportOutput(line, synchronizationContext),
                    runCancellation.Token)
                .ConfigureAwait(true);
            if (!startResult.IsSuccess)
            {
                AddOutput(ProcessOutputChannel.StandardError, startResult.Error.Message);
                StatusText = startResult.Error.Message;
                return;
            }

            debugSession = startResult.Value;
            _debugSession = debugSession;
            AddOutput(
                ProcessOutputChannel.StandardOutput,
                $"[Debug Test] Attach debugger to process {debugSession.ProcessId}.");
            StatusText = $"Waiting for debugger to attach to process {debugSession.ProcessId}…";

            var completion = await debugSession.Completion.ConfigureAwait(true);
            if (!completion.IsSuccess)
            {
                AddOutput(ProcessOutputChannel.StandardError, completion.Error.Message);
                StatusText = completion.Error.Message;
            }
            else
            {
                StatusText = completion.Value.Succeeded
                    ? $"{test.DisplayName} debug session completed."
                    : $"{test.DisplayName} debug session failed.";
            }
        }
        catch (OperationCanceledException) when (runCancellation.IsCancellationRequested)
        {
            StatusText = "Test debug session canceled.";
        }
        finally
        {
            if (ReferenceEquals(_debugSession, debugSession))
            {
                _debugSession = null;
            }

            if (debugSession is not null)
            {
                await debugSession.DisposeAsync().ConfigureAwait(true);
            }

            CompleteRun(runCancellation);
        }
    }

    public void Stop()
    {
        _debugSession?.Terminate();
        _runCancellation?.Cancel();
    }

    public void ClearOutput()
    {
        OutputLines.Clear();
        OnPropertyChanged(nameof(HasOutput));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
    }

    private async Task RunProjectsAsync(
        WorkspaceTestProjectDiscovery[] projects,
        bool isRerun,
        CancellationToken cancellationToken)
    {
        using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _runCancellation = runCancellation;
        IsRunning = true;
        OutputLines.Clear();
        OnPropertyChanged(nameof(HasOutput));
        var synchronizationContext = SynchronizationContext.Current;

        try
        {
            foreach (var project in projects)
            {
                runCancellation.Token.ThrowIfCancellationRequested();
                StatusText = $"Running {project.DisplayName}…";
                AddOutput(ProcessOutputChannel.StandardOutput, $"[Test] {project.DisplayName}");
                var result = await _testRunService
                    .RunAsync(
                        new DotNetTestRunRequest(project.ProjectPath),
                        line => ReportOutput(line, synchronizationContext),
                        runCancellation.Token)
                    .ConfigureAwait(true);
                if (!result.IsSuccess)
                {
                    _failedProjects.Add(project);
                    AddOutput(ProcessOutputChannel.StandardError, result.Error.Message);
                    continue;
                }

                if (!result.Value.Succeeded)
                {
                    _failedProjects.Add(project);
                }
            }

            NotifyFailedProjectsChanged();
            StatusText = _failedProjects.Count == 0
                ? isRerun
                    ? "All previously failed test projects passed."
                    : $"All {projects.Length} test project{(projects.Length == 1 ? string.Empty : "s")} passed."
                : isRerun
                    ? $"{_failedProjects.Count} test project{(_failedProjects.Count == 1 ? string.Empty : "s")} still failing."
                    : $"{_failedProjects.Count} of {projects.Length} test projects failed.";
        }
        catch (OperationCanceledException) when (runCancellation.IsCancellationRequested)
        {
            StatusText = "Test run canceled.";
        }
        finally
        {
            CompleteRun(runCancellation);
        }
    }

    private void CompleteRun(CancellationTokenSource runCancellation)
    {
        if (ReferenceEquals(_runCancellation, runCancellation))
        {
            _runCancellation = null;
        }

        IsRunning = false;
        OnPropertyChanged(nameof(CanRerunFailedProjects));
    }

    private void ClearFailedProjects()
    {
        if (_failedProjects.Count == 0)
        {
            return;
        }

        _failedProjects.Clear();
        NotifyFailedProjectsChanged();
    }

    private void NotifyFailedProjectsChanged()
    {
        OnPropertyChanged(nameof(HasFailedProjects));
        OnPropertyChanged(nameof(CanRerunFailedProjects));
    }

    private void ReportOutput(ProcessOutputLine line, SynchronizationContext? synchronizationContext)
    {
        if (synchronizationContext is null
            || ReferenceEquals(SynchronizationContext.Current, synchronizationContext))
        {
            AddOutput(line.Channel, line.Text);
            return;
        }

        synchronizationContext.Send(
            static state =>
            {
                var report = (OutputReport)state!;
                report.ViewModel.AddOutput(report.Line.Channel, report.Line.Text);
            },
            new OutputReport(this, line));
    }

    private void AddOutput(ProcessOutputChannel channel, string text)
    {
        OutputLines.Add(new TestRunOutputLineViewModel(channel, text));
        OnPropertyChanged(nameof(HasOutput));
    }

    private void NotifyCollectionSummary()
    {
        OnPropertyChanged(nameof(HasProjects));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(TotalTests));
        OnPropertyChanged(nameof(CanRunAll));
        OnPropertyChanged(nameof(CanRerunFailedProjects));
        OnPropertyChanged(nameof(CanDebug));
    }

    private sealed record OutputReport(
        TestExplorerViewModel ViewModel,
        ProcessOutputLine Line);
}
