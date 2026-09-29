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

    private TerminalHostViewModel? Host => DataContext as TerminalHostViewModel;

    private TerminalViewModel? Session => Host?.SelectedSession;

    private void NewSession_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        Host?.AddSession();
    }

    private async void CloseSession_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (Host is { } host)
        {
            await host.CloseSelectedSessionAsync().ConfigureAwait(true);
        }
    }

    private async void Start_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (Session is { } session)
        {
            await session.StartAsync().ConfigureAwait(true);
        }
    }

    private async void Stop_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (Session is { } session)
        {
            await session.StopAsync().ConfigureAwait(true);
        }
    }

    private void Clear_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        Session?.Clear();
    }

    private async void Submit_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (Session is { } session)
        {
            await session.SubmitAsync().ConfigureAwait(true);
        }
    }

    private async void Command_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.Enter && Session is { CanSubmit: true } session)
        {
            eventArgs.Handled = true;
            await session.SubmitAsync().ConfigureAwait(true);
        }
    }
}
