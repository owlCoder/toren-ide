using Avalonia.Controls;
using Avalonia.Interactivity;
using Toren.App.Execution.ViewModels;
using Toren.DotNet.Execution.Models;

namespace Toren.App.Views.Output;

internal sealed partial class OutputPanel : UserControl
{
    public OutputPanel()
    {
        InitializeComponent();
    }

    private WorkspaceExecutionViewModel? ViewModel => DataContext as WorkspaceExecutionViewModel;

    private async void Restore_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.ExecuteAsync(DotNetCommandKind.Restore).ConfigureAwait(true);
        }
    }

    private async void Build_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.ExecuteAsync(DotNetCommandKind.Build).ConfigureAwait(true);
        }
    }

    private async void Rebuild_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.ExecuteAsync(DotNetCommandKind.Rebuild).ConfigureAwait(true);
        }
    }

    private async void Clean_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.ExecuteAsync(DotNetCommandKind.Clean).ConfigureAwait(true);
        }
    }

    private async void Publish_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.ExecuteAsync(DotNetCommandKind.Publish).ConfigureAwait(true);
        }
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        ViewModel?.Cancel();
    }

    private void Clear_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        ViewModel?.ClearOutput();
    }
}
