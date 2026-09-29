using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Toren.App.Debugging.ViewModels;
using Toren.Debugging.Models;

namespace Toren.App.Views.Debugging;

internal sealed partial class DebugPanel : UserControl
{
    public DebugPanel()
    {
        InitializeComponent();
    }

    internal Func<DebugStackFrame, Task>? NavigateFrameAsync { get; set; }

    internal Func<Task>? AddBreakpointAtCaretAsync { get; set; }

    internal Func<Task>? RunToCursorAtCaretAsync { get; set; }

    private DebugSessionViewModel? ViewModel => DataContext as DebugSessionViewModel;

    private async void Continue_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.ContinueAsync().ConfigureAwait(true);
        }
    }

    private async void Pause_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.PauseAsync().ConfigureAwait(true);
        }
    }

    private async void StepOver_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.StepOverAsync().ConfigureAwait(true);
        }
    }

    private async void StepInto_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.StepIntoAsync().ConfigureAwait(true);
        }
    }

    private async void StepOut_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.StepOutAsync().ConfigureAwait(true);
        }
    }

    private async void RunToCursor_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (RunToCursorAtCaretAsync is { } runToCursorAsync)
        {
            await runToCursorAsync().ConfigureAwait(true);
        }
    }

    private async void Restart_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.RestartAsync().ConfigureAwait(true);
        }
    }

    private async void Stop_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.StopAsync().ConfigureAwait(true);
        }
    }

    private async void StackFrames_OnSelectionChanged(object? sender, SelectionChangedEventArgs eventArgs)
    {
        if (sender is ListBox { SelectedItem: DebugStackFrame frame }
            && ViewModel is { } viewModel)
        {
            await viewModel.SelectFrameAsync(frame).ConfigureAwait(true);
        }
    }

    private async void StackFrames_OnDoubleTapped(object? sender, TappedEventArgs eventArgs)
    {
        if (sender is ListBox { SelectedItem: DebugStackFrame frame }
            && NavigateFrameAsync is { } navigateFrameAsync)
        {
            await navigateFrameAsync(frame).ConfigureAwait(true);
        }
    }

    private async void AddWatch_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.AddWatchAsync().ConfigureAwait(true);
        }
    }

    private async void WatchExpression_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.Enter && ViewModel is { } viewModel)
        {
            eventArgs.Handled = true;
            await viewModel.AddWatchAsync().ConfigureAwait(true);
        }
    }

    private void RemoveWatch_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button { DataContext: DebugWatchItemViewModel item }
            && ViewModel is { } viewModel)
        {
            viewModel.RemoveWatch(item);
        }
    }

    private async void AddBreakpoint_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (AddBreakpointAtCaretAsync is { } addBreakpointAsync)
        {
            await addBreakpointAsync().ConfigureAwait(true);
        }
    }

    private async void RemoveBreakpoint_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button { DataContext: DebugBreakpointItemViewModel item }
            && ViewModel is { } viewModel)
        {
            await viewModel.RemoveBreakpointAsync(item).ConfigureAwait(true);
        }
    }

    private async void EvaluateConsole_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.EvaluateConsoleAsync().ConfigureAwait(true);
        }
    }

    private async void ConsoleExpression_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.Enter && ViewModel is { } viewModel)
        {
            eventArgs.Handled = true;
            await viewModel.EvaluateConsoleAsync().ConfigureAwait(true);
        }
    }

    private void ClearConsole_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        ViewModel?.ClearConsole();
    }
}
