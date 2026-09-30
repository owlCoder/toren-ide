using System.ComponentModel;
using Avalonia.Controls;
using Toren.App.Packages.ViewModels;
using Toren.App.Shell;
using Toren.App.ViewModels;
using Toren.App.Views.Packages;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Models;

namespace Toren.App.Packages.Services;

internal sealed class PackageManagerController
{
    private readonly Window _window;
    private readonly MainWindowViewModel _shell;
    private readonly IWorkspaceClassifier _workspaceClassifier;
    private readonly IWorkspaceProjectGraphService _projectGraphService;
    private readonly PackageManagerViewModel _viewModel;
    private readonly Action<string> _setStatus;
    private readonly ToolDialogHost _dialog;
    private readonly Button _packagesButton;
    private CancellationTokenSource? _loadCancellation;
    private bool _detached;

    private PackageManagerController(
        Window window,
        MainWindowViewModel shell,
        IWorkspaceClassifier workspaceClassifier,
        IWorkspaceProjectGraphService projectGraphService,
        PackageManagerViewModel viewModel,
        Action<string> setStatus)
    {
        _window = window;
        _shell = shell;
        _workspaceClassifier = workspaceClassifier;
        _projectGraphService = projectGraphService;
        _viewModel = viewModel;
        _setStatus = setStatus;
        _dialog = new ToolDialogHost(window, "Packages", new PackageManagerPanel { DataContext = viewModel }, 960, 680);
        _packagesButton = window.FindControl<Button>("PackagesActivityButton")!;
        _packagesButton.IsEnabled = true;
        _packagesButton.Click += PackagesButton_OnClick;

        _shell.PropertyChanged += Shell_OnPropertyChanged;
        _shell.Explorer.PropertyChanged += Explorer_OnPropertyChanged;
        _window.Closed += Window_OnClosed;
        SynchronizeWorkspace();
    }

    public static void Attach(
        Window window,
        MainWindowViewModel shell,
        IWorkspaceClassifier workspaceClassifier,
        IWorkspaceProjectGraphService projectGraphService,
        PackageManagerViewModel viewModel,
        Action<string> setStatus)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(workspaceClassifier);
        ArgumentNullException.ThrowIfNull(projectGraphService);
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(setStatus);

        _ = new PackageManagerController(window, shell, workspaceClassifier, projectGraphService, viewModel, setStatus);
    }

    private void PackagesButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs args) => _dialog.Open();

    private void Shell_OnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(MainWindowViewModel.WorkspacePath))
        {
            SynchronizeWorkspace();
        }
    }

    private void Explorer_OnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(ExplorerViewModel.IsWorkspaceOpen))
        {
            SynchronizeWorkspace();
        }
    }

    private void SynchronizeWorkspace()
    {
        CancelLoad();
        if (_detached || !_shell.Explorer.IsWorkspaceOpen)
        {
            _viewModel.SetWorkspace(null, []);
            return;
        }

        var workspacePath = Path.GetFullPath(_shell.WorkspacePath);
        var workingDirectory = Directory.Exists(workspacePath)
            ? workspacePath
            : Path.GetDirectoryName(workspacePath);
        if (workingDirectory is null || !TryResolveDescriptor(workspacePath, out var descriptor))
        {
            _viewModel.SetWorkspace(workingDirectory, []);
            return;
        }

        var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;
        _ = LoadProjectsAsync(descriptor!, workingDirectory, cancellation);
    }

    private bool TryResolveDescriptor(string workspacePath, out WorkspaceDescriptor? descriptor)
    {
        if (Directory.Exists(workspacePath))
        {
            descriptor = _workspaceClassifier.ClassifyDirectory(workspacePath);
            return true;
        }

        return _workspaceClassifier.TryClassifyFile(workspacePath, out descriptor);
    }

    private async Task LoadProjectsAsync(
        WorkspaceDescriptor workspace,
        string workingDirectory,
        CancellationTokenSource cancellation)
    {
        try
        {
            var result = await _projectGraphService
                .LoadAsync(workspace, cancellation.Token)
                .ConfigureAwait(true);
            if (_detached || cancellation.IsCancellationRequested || !ReferenceEquals(_loadCancellation, cancellation))
            {
                return;
            }

            if (result.IsFailure)
            {
                _viewModel.SetWorkspace(workingDirectory, []);
                _viewModel.StatusText = result.Error.Message;
                _setStatus(result.Error.Message);
                return;
            }

            _viewModel.SetWorkspace(workingDirectory, result.Value!.Projects);
            await _viewModel.RefreshInstalledAsync(cancellation.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
    }

    private void Window_OnClosed(object? sender, EventArgs eventArgs) => Detach();

    private void CancelLoad()
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = null;
    }

    private void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        CancelLoad();
        _shell.PropertyChanged -= Shell_OnPropertyChanged;
        _shell.Explorer.PropertyChanged -= Explorer_OnPropertyChanged;
        _window.Closed -= Window_OnClosed;
        _packagesButton.Click -= PackagesButton_OnClick;
    }
}
