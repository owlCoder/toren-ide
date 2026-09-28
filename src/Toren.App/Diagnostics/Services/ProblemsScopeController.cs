using System.ComponentModel;
using Avalonia.Controls;
using Toren.App.Diagnostics.Contracts;
using Toren.App.Diagnostics.Models;
using Toren.App.ViewModels;

namespace Toren.App.Diagnostics.Services;

internal sealed class ProblemsScopeController
{
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private readonly Window _window;
    private readonly MainWindowViewModel _viewModel;
    private readonly IProblemsWorkspaceScopeService _scopeService;
    private readonly Action<string> _setStatus;
    private ProblemsWorkspaceScopeIndex _scopeIndex = ProblemsWorkspaceScopeIndex.Empty;
    private CancellationTokenSource? _refreshCancellation;
    private bool _detached;

    private ProblemsScopeController(
        Window window,
        MainWindowViewModel viewModel,
        IProblemsWorkspaceScopeService scopeService,
        Action<string> setStatus)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _scopeService = scopeService ?? throw new ArgumentNullException(nameof(scopeService));
        _setStatus = setStatus ?? throw new ArgumentNullException(nameof(setStatus));

        _window.Closed += Window_OnClosed;
        _viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
        _viewModel.Documents.PropertyChanged += Documents_OnPropertyChanged;
        ApplyScopeContext();
    }

    public static void Attach(
        Window window,
        MainWindowViewModel viewModel,
        IProblemsWorkspaceScopeService scopeService,
        Action<string> setStatus)
    {
        _ = new ProblemsScopeController(window, viewModel, scopeService, setStatus);
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(MainWindowViewModel.WorkspacePath))
        {
            QueueWorkspaceRefresh();
        }
    }

    private void Documents_OnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(DocumentHostViewModel.ActiveDocument))
        {
            ApplyScopeContext();
        }
    }

    private void QueueWorkspaceRefresh()
    {
        _refreshCancellation?.Cancel();
        _scopeIndex = ProblemsWorkspaceScopeIndex.Empty;
        ApplyScopeContext();

        if (!_viewModel.Explorer.IsWorkspaceOpen || string.IsNullOrWhiteSpace(_viewModel.WorkspacePath))
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        _refreshCancellation = cancellation;
        _ = RefreshWorkspaceAsync(_viewModel.WorkspacePath, cancellation);
    }

    private async Task RefreshWorkspaceAsync(string workspacePath, CancellationTokenSource cancellation)
    {
        try
        {
            var result = await _scopeService.BuildAsync(workspacePath, cancellation.Token).ConfigureAwait(true);
            if (_detached || cancellation.IsCancellationRequested)
            {
                return;
            }

            if (result.IsSuccess)
            {
                _scopeIndex = result.Value;
                ApplyScopeContext();
            }
            else
            {
                _setStatus(result.Error.Message);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            if (ReferenceEquals(_refreshCancellation, cancellation))
            {
                _refreshCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private void ApplyScopeContext()
    {
        if (_detached)
        {
            return;
        }

        var currentDocumentPath = _viewModel.Documents.ActiveDocument?.Path;
        ProblemsProjectScope? projectScope = null;
        if (!string.IsNullOrWhiteSpace(currentDocumentPath))
        {
            var fullPath = Path.GetFullPath(currentDocumentPath);
            projectScope = _scopeIndex.Projects.FirstOrDefault(project =>
                project.FilePaths.Any(path => PathComparer.Equals(path, fullPath)));
        }

        _viewModel.Problems.SetScopeContext(new ProblemsScopeContext(
            currentDocumentPath,
            projectScope?.DisplayName,
            projectScope?.FilePaths ?? []));
    }

    private void Window_OnClosed(object? sender, EventArgs eventArgs)
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        _refreshCancellation?.Cancel();
        _window.Closed -= Window_OnClosed;
        _viewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        _viewModel.Documents.PropertyChanged -= Documents_OnPropertyChanged;
    }
}
