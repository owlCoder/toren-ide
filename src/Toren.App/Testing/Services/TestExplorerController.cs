using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using AvaloniaEdit;
using Toren.App.Testing.Contracts;
using Toren.App.Testing.ViewModels;
using Toren.App.ViewModels;
using Toren.App.Views.Testing;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Models;

namespace Toren.App.Testing.Services;

internal sealed class TestExplorerController
{
    private readonly Window _window;
    private readonly MainWindowViewModel _shell;
    private readonly IWorkspaceClassifier _workspaceClassifier;
    private readonly IWorkspaceTestDiscoveryService _testDiscoveryService;
    private readonly TestExplorerViewModel _viewModel;
    private readonly Action<string> _setStatus;
    private readonly TextEditor _editor;
    private readonly Button _explorerButton;
    private readonly Button _testsButton;
    private readonly Border _explorerPanel;
    private readonly Control? _explorerContent;
    private readonly TestExplorerPanel _testPanel;
    private readonly Button? _refreshButton;
    private CancellationTokenSource? _refreshCancellation;
    private bool _testsVisible;
    private bool _detached;

    private TestExplorerController(
        Window window,
        MainWindowViewModel shell,
        IWorkspaceClassifier workspaceClassifier,
        IWorkspaceTestDiscoveryService testDiscoveryService,
        TestExplorerViewModel viewModel,
        Action<string> setStatus,
        Button explorerButton,
        Button testsButton,
        Border explorerPanel)
    {
        _window = window;
        _shell = shell;
        _workspaceClassifier = workspaceClassifier;
        _testDiscoveryService = testDiscoveryService;
        _viewModel = viewModel;
        _setStatus = setStatus;
        _explorerButton = explorerButton;
        _testsButton = testsButton;
        _explorerPanel = explorerPanel;
        _explorerContent = explorerPanel.Child;
        _editor = window.FindControl<TextEditor>("DocumentEditor")
            ?? throw new InvalidOperationException("The document editor could not be located.");
        _testPanel = new TestExplorerPanel
        {
            DataContext = viewModel,
            NavigateOutputAsync = NavigateOutputAsync,
        };
        _refreshButton = _testPanel.FindControl<Button>("RefreshTestsButton");

        _testsButton.IsEnabled = true;
        ToolTip.SetTip(_testsButton, "Tests");
        _explorerButton.Click += ExplorerButton_OnClick;
        _testsButton.Click += TestsButton_OnClick;
        if (_refreshButton is not null)
        {
            _refreshButton.Click += RefreshButton_OnClick;
        }

        _shell.PropertyChanged += Shell_OnPropertyChanged;
        _shell.Explorer.PropertyChanged += Explorer_OnPropertyChanged;
        _window.Closed += Window_OnClosed;
    }

    public static void Attach(
        Window window,
        MainWindowViewModel shell,
        IWorkspaceClassifier workspaceClassifier,
        IWorkspaceTestDiscoveryService testDiscoveryService,
        TestExplorerViewModel viewModel,
        Action<string> setStatus)
    {
        ArgumentNullException.ThrowIfNull(window);
        var explorerButton = window.FindControl<Button>("ExplorerActivityButton");
        var testsButton = window.FindControl<Button>("TestsActivityButton");
        var explorerPanel = window.FindControl<Border>("ExplorerPanel");
        if (explorerButton is null || testsButton is null || explorerPanel is null)
        {
            return;
        }

        _ = new TestExplorerController(
            window,
            shell,
            workspaceClassifier,
            testDiscoveryService,
            viewModel,
            setStatus,
            explorerButton,
            testsButton,
            explorerPanel);
    }

    private void ExplorerButton_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        ShowExplorer();
    }

    private async void TestsButton_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        _testsVisible = true;
        _explorerButton.Classes.Remove("active");
        if (!_testsButton.Classes.Contains("active"))
        {
            _testsButton.Classes.Add("active");
        }

        _explorerPanel.Child = _testPanel;
        await RefreshAsync().ConfigureAwait(true);
    }

    private async void RefreshButton_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        await RefreshAsync().ConfigureAwait(true);
    }

    private void Shell_OnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(MainWindowViewModel.WorkspacePath) && _testsVisible)
        {
            _ = RefreshAsync();
        }
    }

    private void Explorer_OnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName != nameof(ExplorerViewModel.IsWorkspaceOpen))
        {
            return;
        }

        if (!_shell.Explorer.IsWorkspaceOpen)
        {
            CancelRefresh();
            _viewModel.Reset();
        }
        else if (_testsVisible)
        {
            _ = RefreshAsync();
        }
    }

    private async Task RefreshAsync()
    {
        CancelRefresh();
        var workspace = ResolveWorkspace();
        if (workspace is null)
        {
            _viewModel.Reset();
            return;
        }

        var cancellation = new CancellationTokenSource();
        _refreshCancellation = cancellation;
        _viewModel.BeginRefresh();
        try
        {
            var result = await _testDiscoveryService
                .DiscoverAsync(workspace, cancellation.Token)
                .ConfigureAwait(true);
            if (!ReferenceEquals(_refreshCancellation, cancellation))
            {
                return;
            }

            if (result.IsSuccess)
            {
                _viewModel.Replace(result.Value);
            }
            else
            {
                _viewModel.SetError(result.Error.Message);
                _setStatus(result.Error.Message);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // A newer workspace or refresh superseded this discovery pass.
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

    private async Task NavigateOutputAsync(TestRunOutputLineViewModel outputLine)
    {
        if (outputLine.Location is not { } location)
        {
            return;
        }

        var filePath = Path.GetFullPath(location.FilePath);
        if (!File.Exists(filePath))
        {
            _setStatus($"Test source file not found: {filePath}");
            return;
        }

        var opened = await _shell.Documents.OpenAsync(filePath).ConfigureAwait(true);
        if (!opened.IsSuccess)
        {
            _setStatus(opened.Error.Message);
            return;
        }

        await _shell.ActivateDocumentAsync(opened.Value).ConfigureAwait(true);
        var line = Math.Clamp(location.Line, 1, _editor.Document.LineCount);
        var documentLine = _editor.Document.GetLineByNumber(line);
        var column = Math.Clamp(location.Column, 1, documentLine.Length + 1);
        _editor.CaretOffset = documentLine.Offset + column - 1;
        _editor.ScrollTo(line, column);
        _editor.Focus();
        _setStatus($"Opened {opened.Value.Title}:{line}");
    }

    private WorkspaceDescriptor? ResolveWorkspace()
    {
        if (!_shell.Explorer.IsWorkspaceOpen)
        {
            return null;
        }

        var path = _shell.WorkspacePath;
        if (Directory.Exists(path))
        {
            return _workspaceClassifier.ClassifyDirectory(path);
        }

        return _workspaceClassifier.TryClassifyFile(path, out var workspace)
            ? workspace
            : null;
    }

    private void ShowExplorer()
    {
        if (!_testsVisible)
        {
            return;
        }

        _testsVisible = false;
        _testsButton.Classes.Remove("active");
        if (!_explorerButton.Classes.Contains("active"))
        {
            _explorerButton.Classes.Add("active");
        }

        _explorerPanel.Child = _explorerContent;
        CancelRefresh();
    }

    private void CancelRefresh()
    {
        _refreshCancellation?.Cancel();
        _refreshCancellation = null;
    }

    private void Window_OnClosed(object? sender, EventArgs eventArgs)
    {
        Detach();
    }

    private void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        CancelRefresh();
        _testPanel.NavigateOutputAsync = null;
        _explorerButton.Click -= ExplorerButton_OnClick;
        _testsButton.Click -= TestsButton_OnClick;
        if (_refreshButton is not null)
        {
            _refreshButton.Click -= RefreshButton_OnClick;
        }

        _shell.PropertyChanged -= Shell_OnPropertyChanged;
        _shell.Explorer.PropertyChanged -= Explorer_OnPropertyChanged;
        _window.Closed -= Window_OnClosed;
    }
}
