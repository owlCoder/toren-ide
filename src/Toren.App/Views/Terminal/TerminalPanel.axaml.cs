using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Toren.App.Terminal.ViewModels;

namespace Toren.App.Views.Terminal;

internal sealed partial class TerminalPanel : UserControl
{
    public TerminalPanel()
    {
        InitializeComponent();
    }

    private TerminalViewModel? ViewModel => DataContext as TerminalViewModel;

    private async void Start_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.StartAsync().ConfigureAwait(true);
        }
    }

    private async void Stop_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.StopAsync().ConfigureAwait(true);
        }
    }

    private void Clear_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        ViewModel?.Clear();
    }

    private async void Submit_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.SubmitAsync().ConfigureAwait(true);
        }
    }

    private async void Command_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.Enter && ViewModel is { CanSubmit: true } viewModel)
        {
            eventArgs.Handled = true;
            await viewModel.SubmitAsync().ConfigureAwait(true);
        }
    }
}
