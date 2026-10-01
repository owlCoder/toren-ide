using System.ComponentModel;
using Avalonia.Controls;
using Toren.App.Packages.ViewModels;
using Toren.App.Shell;
using Toren.App.ViewModels;
using Toren.App.Views.Packages;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Models;
using Toren.Workspaces.Services;

namespace Toren.App.Packages.Services;

internal sealed class PackageManagerController
{
    private readonly Window _window;
    private readonly MainWindowViewModel _shell;
    private readonly IWorkspaceClassifier _workspaceClassifier;
    private readonly IWorkspaceProjectCatalog _projectCatalog;
    private readonly PackageManagerViewModel _viewModel;
    private readonly Action<string> _setStatus;
    private readonly ToolDialogHost _dialog;
    private readonly Button _packagesButton;
    private CancellationTokenSource? _loadCancellation;
    private bool _installedPackagesPending;
    private bool _detached;

    private PackageManagerController(
        Window window,
        MainWindowViewModel shell,
        IWorkspaceClassifier workspaceClassifier,
        IWorkspaceProjectCatalog projectCatalog,
        PackageManagerViewModel viewModel,
        Action<string> setStatus)
    {
        _window = window;
        _shell = shell;
        _workspaceClassifier = workspaceClassifier;
        _projectCatalog = projectCatalog;
        _viewModel = viewModel;
        _setStatus = setStatus;
        _dialog = new ToolDialogHost(window, "Packages", new PackageManagerPanel { DataContext = viewModel }, 960, 680, "PackageSearchBox");
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
        IWorkspaceProjectCatalog projectCatalog,
        PackageManagerViewModel viewModel,
        Action<string> setStatus)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(workspaceClassifier);
        ArgumentNullException.ThrowIfNull(projectCatalog);
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(setStatus);

        _ = new PackageManagerController(window, shell, workspaceClassifier, projectCatalog, viewModel, setStatus);
    }

    private async void PackagesButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs args)
    {
        _dialog.Open();
        if (_installedPackagesPending && _loadCancellation is { } cancellation)
        {
            await RefreshInstalledAsync(cancellation).ConfigureAwait(true);
        }
    }

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
        _installedPackagesPending = false;
        if (_detached || !_shell.Explorer.IsWorkspaceOpen)
        {
            _viewModel.SetWorkspace(null, []);
            return;
        }

        var workspacePath = Path.GetFullPath(_shell.WorkspacePath);
        var workingDirectory = Directory.Exists(workspacePath)
            ? workspacePath
            : Path.GetDirectoryName(workspacePath);
        if (workingDirectory is null || _workspaceClassifier.ClassifyPath(workspacePath) is not { } descriptor)
        {
            _viewModel.SetWorkspace(workingDirectory, []);
            return;
        }

        var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;
        _viewModel.SetWorkspace(workingDirectory, []);
        _viewModel.StatusText = "Loading workspace projects…";
        _ = LoadProjectsAsync(descriptor, workingDirectory, cancellation);
    }

    private async Task LoadProjectsAsync(
        WorkspaceDescriptor workspace,
        string workingDirectory,
        CancellationTokenSource cancellation)
    {
        try
        {
            var result = await _projectCatalog
                .GetProjectsAsync(workspace, cancellation.Token)
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

            _viewModel.SetWorkspace(workingDirectory, result.Value!);
            // Listing installed packages starts an SDK process; wait until the dialog is in use.
            _installedPackagesPending = true;
            if (_dialog.IsOpen)
            {
                await RefreshInstalledAsync(cancellation).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
    }

    private async Task RefreshInstalledAsync(CancellationTokenSource cancellation)
    {
        _installedPackagesPending = false;
        try
        {
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
