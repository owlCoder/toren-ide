using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using AvaloniaEdit;
using Toren.App.Debugging.ViewModels;
using Toren.App.ViewModels;
using Toren.App.Views.Debugging;
using Toren.Debugging.Models;

namespace Toren.App.Debugging.Services;

internal sealed class DebugSessionController
{
    private readonly Window _window;
    private readonly MainWindowViewModel _shell;
    private readonly DebugSessionCoordinator _coordinator;
    private readonly DebugSessionViewModel _viewModel;
    private readonly Action<string> _setStatus;
    private readonly TextEditor _editor;
    private readonly Button _debugButton;
    private readonly TabControl _toolTabs;
    private readonly TabItem _debugTab;
    private readonly DebugPanel _panel;
    private CancellationTokenSource? _monitorCancellation;
    private bool _monitorRunning;
    private bool _detached;

    private DebugSessionController(
        Window window,
        MainWindowViewModel shell,
        DebugSessionCoordinator coordinator,
        DebugSessionViewModel viewModel,
        Action<string> setStatus,
        Button debugButton,
        TabControl toolTabs)
    {
        _window = window;
        _shell = shell;
        _coordinator = coordinator;
        _viewModel = viewModel;
        _setStatus = setStatus;
        _debugButton = debugButton;
        _toolTabs = toolTabs;
        _editor = window.FindControl<TextEditor>("DocumentEditor")
            ?? throw new InvalidOperationException("The document editor could not be located.");
        _panel = new DebugPanel
        {
            DataContext = viewModel,
            NavigateFrameAsync = NavigateFrameAsync,
            AddBreakpointAtCaretAsync = AddBreakpointAtCaretAsync,
            RunToCursorAtCaretAsync = RunToCursorAtCaretAsync,
        };
        _debugTab = new TabItem
        {
            Header = "Debug",
            Content = _panel,
        };
        _toolTabs.Items.Add(_debugTab);

        _debugButton.IsEnabled = true;
        ToolTip.SetTip(_debugButton, "Debug");
        _debugButton.Click += DebugButton_OnClick;
        _coordinator.StateChanged += Coordinator_OnStateChanged;
        _window.KeyDown += Window_OnKeyDown;
        _window.Closed += Window_OnClosed;
        SynchronizeSession();
    }

    public static void Attach(
        Window window,
        MainWindowViewModel shell,
        DebugSessionCoordinator coordinator,
        DebugSessionViewModel viewModel,
        Action<string> setStatus)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(setStatus);

        var activityRail = window.GetLogicalDescendants()
            .OfType<Border>()
            .FirstOrDefault(control => control.Classes.Contains("activity-rail"));
        var debugButton = activityRail?.GetLogicalDescendants()
            .OfType<Button>()
            .FirstOrDefault(control =>
                control.Classes.Contains("activity-button")
                && Grid.GetRow(control) == 3);
        var toolTabs = window.GetLogicalDescendants()
            .OfType<TabControl>()
            .FirstOrDefault(control => control.Classes.Contains("tool-tabs"));
        if (debugButton is null || toolTabs is null)
        {
            return;
        }

        _ = new DebugSessionController(
            window,
            shell,
            coordinator,
            viewModel,
            setStatus,
            debugButton,
            toolTabs);
    }

    private void DebugButton_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        _toolTabs.SelectedItem = _debugTab;
        _setStatus(_viewModel.StatusText);
    }

    private async void Window_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key != Key.F9 || eventArgs.KeyModifiers != KeyModifiers.None)
        {
            return;
        }

        var activeDocument = _shell.Documents.ActiveDocument;
        if (activeDocument is null)
        {
            _setStatus("Open a source document before toggling a breakpoint.");
            eventArgs.Handled = true;
            return;
        }

        var location = _editor.Document.GetLocation(_editor.CaretOffset);
        var added = await _viewModel
            .ToggleBreakpointAsync(activeDocument.Path, location.Line)
            .ConfigureAwait(true);
        _setStatus(added
            ? $"Breakpoint set at {activeDocument.Title}:{location.Line}."
            : $"Breakpoint removed from {activeDocument.Title}:{location.Line}.");
        eventArgs.Handled = true;
    }

    private void Coordinator_OnStateChanged(object? sender, EventArgs eventArgs)
    {
        Dispatcher.UIThread.Post(SynchronizeSession);
    }

    private void SynchronizeSession()
    {
        if (_detached)
        {
            return;
        }

        _viewModel.RefreshSessionState();
        if (!_coordinator.IsAttached)
        {
            CancelMonitor();
            _viewModel.ResetSession();
            return;
        }

        _ = _viewModel.ApplyAllBreakpointsAsync();
        StartMonitor();
    }

    private void StartMonitor()
    {
        if (_monitorRunning || !_coordinator.IsAttached)
        {
            return;
        }

        CancelMonitor();
        var cancellation = new CancellationTokenSource();
        _monitorCancellation = cancellation;
        _monitorRunning = true;
        _ = MonitorStopsAsync(cancellation);
    }

    private async Task MonitorStopsAsync(CancellationTokenSource cancellation)
    {
        try
        {
            while (!cancellation.IsCancellationRequested && _coordinator.IsAttached)
            {
                var stopped = await _viewModel
                    .WaitForStopAndRefreshAsync(cancellation.Token)
                    .ConfigureAwait(true);
                if (stopped.IsFailure)
                {
                    if (!cancellation.IsCancellationRequested && _coordinator.IsAttached)
                    {
                        _setStatus(stopped.Error.Message);
                    }

                    break;
                }

                _toolTabs.SelectedItem = _debugTab;
                _setStatus(_viewModel.StatusText);
                while (!cancellation.IsCancellationRequested
                       && _coordinator.IsAttached
                       && _coordinator.StoppedThreadId.HasValue)
                {
                    await Task.Delay(50, cancellation.Token).ConfigureAwait(true);
                }
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // A session state change or window close superseded this monitor.
        }
        finally
        {
            if (ReferenceEquals(_monitorCancellation, cancellation))
            {
                _monitorCancellation = null;
            }

            _monitorRunning = false;
            cancellation.Dispose();
            if (!_detached && _coordinator.IsAttached && !_coordinator.StoppedThreadId.HasValue)
            {
                StartMonitor();
            }
        }
    }

    private async Task NavigateFrameAsync(DebugStackFrame frame)
    {
        if (string.IsNullOrWhiteSpace(frame.SourcePath) || frame.Line is not > 0)
        {
            _setStatus("The selected stack frame has no source location.");
            return;
        }

        await NavigateToSourceAsync(frame.SourcePath, frame.Line.Value, frame.Column ?? 1)
            .ConfigureAwait(true);
    }

    private async Task AddBreakpointAtCaretAsync()
    {
        var activeDocument = _shell.Documents.ActiveDocument;
        if (activeDocument is null)
        {
            _setStatus("Open a source document before adding a breakpoint.");
            return;
        }

        var location = _editor.Document.GetLocation(_editor.CaretOffset);
        var condition = _viewModel.BreakpointCondition;
        await _viewModel
            .AddBreakpointAsync(activeDocument.Path, location.Line, condition)
            .ConfigureAwait(true);
        _viewModel.BreakpointCondition = string.Empty;
        _setStatus($"Breakpoint set at {activeDocument.Title}:{location.Line}.");
    }

    private async Task RunToCursorAtCaretAsync()
    {
        var activeDocument = _shell.Documents.ActiveDocument;
        if (activeDocument is null)
        {
            _setStatus("Open a source document before running to cursor.");
            return;
        }

        var location = _editor.Document.GetLocation(_editor.CaretOffset);
        await _viewModel
            .RunToCursorAsync(activeDocument.Path, location.Line, location.Column)
            .ConfigureAwait(true);
        _setStatus(_viewModel.StatusText);
    }

    private async Task NavigateToSourceAsync(string sourcePath, int line, int column)
    {
        var filePath = Path.GetFullPath(sourcePath);
        if (!File.Exists(filePath))
        {
            _setStatus($"Debug source file not found: {filePath}");
            return;
        }

        var opened = await _shell.Documents.OpenAsync(filePath).ConfigureAwait(true);
        if (!opened.IsSuccess)
        {
            _setStatus(opened.Error.Message);
            return;
        }

        await _shell.ActivateDocumentAsync(opened.Value).ConfigureAwait(true);
        var targetLine = Math.Clamp(line, 1, _editor.Document.LineCount);
        var documentLine = _editor.Document.GetLineByNumber(targetLine);
        var targetColumn = Math.Clamp(column, 1, documentLine.Length + 1);
        _editor.CaretOffset = documentLine.Offset + targetColumn - 1;
        _editor.ScrollTo(targetLine, targetColumn);
        _editor.Focus();
        _setStatus($"Opened {opened.Value.Title}:{targetLine}");
    }

    private void CancelMonitor()
    {
        _monitorCancellation?.Cancel();
        _monitorCancellation = null;
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
        CancelMonitor();
        _debugButton.Click -= DebugButton_OnClick;
        _coordinator.StateChanged -= Coordinator_OnStateChanged;
        _window.KeyDown -= Window_OnKeyDown;
        _window.Closed -= Window_OnClosed;
        _panel.NavigateFrameAsync = null;
        _panel.AddBreakpointAtCaretAsync = null;
        _panel.RunToCursorAtCaretAsync = null;
        _toolTabs.Items.Remove(_debugTab);
    }
}
