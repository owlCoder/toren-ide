using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Toren.App.Testing.Models;
using Toren.Core.Execution.Models;
using Toren.DotNet.Testing.Contracts;
using Toren.DotNet.Testing.Models;

namespace Toren.App.Testing.ViewModels;

public sealed partial class TestExplorerViewModel(IDotNetTestRunService testRunService) : ObservableObject, IDisposable
{
    private readonly IDotNetTestRunService _testRunService = testRunService
        ?? throw new ArgumentNullException(nameof(testRunService));
    private CancellationTokenSource? _runCancellation;
    private bool _disposed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRunAll))]
    [NotifyPropertyChangedFor(nameof(CanStop))]
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

    public bool HasOutput => OutputLines.Count > 0;

    public void BeginRefresh()
    {
        IsLoading = true;
        StatusText = "Discovering tests…";
        OnPropertyChanged(nameof(CanRunAll));
    }

    public void Replace(IReadOnlyList<WorkspaceTestProjectDiscovery> projects)
    {
        ArgumentNullException.ThrowIfNull(projects);
        Projects.Clear();
        foreach (var project in projects)
        {
            Projects.Add(project);
        }

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
        IsLoading = false;
        StatusText = message;
        NotifyCollectionSummary();
    }

    public void Reset()
    {
        Stop();
        Projects.Clear();
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

        using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _runCancellation = runCancellation;
        IsRunning = true;
        OutputLines.Clear();
        OnPropertyChanged(nameof(HasOutput));
        var synchronizationContext = SynchronizationContext.Current;
        var failedProjects = 0;

        try
        {
            foreach (var project in Projects)
            {
                runCancellation.Token.ThrowIfCancellationRequested();
                StatusText = $"Running {project.DisplayName}…";
                AddOutput(
                    ProcessOutputChannel.StandardOutput,
                    $"[Test] {project.DisplayName}");
                var result = await _testRunService
                    .RunAsync(
                        new DotNetTestRunRequest(project.ProjectPath),
                        line => ReportOutput(line, synchronizationContext),
                        runCancellation.Token)
                    .ConfigureAwait(true);
                if (!result.IsSuccess)
                {
                    failedProjects++;
                    AddOutput(ProcessOutputChannel.StandardError, result.Error.Message);
                    continue;
                }

                if (!result.Value.Succeeded)
                {
                    failedProjects++;
                }
            }

            StatusText = failedProjects == 0
                ? $"All {Projects.Count} test project{(Projects.Count == 1 ? string.Empty : "s")} passed."
                : $"{failedProjects} of {Projects.Count} test projects failed.";
        }
        catch (OperationCanceledException) when (runCancellation.IsCancellationRequested)
        {
            StatusText = "Test run canceled.";
        }
        finally
        {
            if (ReferenceEquals(_runCancellation, runCancellation))
            {
                _runCancellation = null;
            }

            IsRunning = false;
        }
    }

    public void Stop()
    {
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
    }

    private sealed record OutputReport(
        TestExplorerViewModel ViewModel,
        ProcessOutputLine Line);
}
