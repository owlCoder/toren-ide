using Avalonia.Controls;
using Avalonia.Interactivity;
using Toren.App.Http.ViewModels;

namespace Toren.App.Views.Http;

internal sealed partial class HttpClientPanel : UserControl
{
    public HttpClientPanel()
    {
        InitializeComponent();
    }

    private HttpClientViewModel? ViewModel => DataContext as HttpClientViewModel;

    private async void Send_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.SendSelectedAsync().ConfigureAwait(true);
        }
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs eventArgs) => ViewModel?.Cancel();

    private void ClearHistory_OnClick(object? sender, RoutedEventArgs eventArgs) => ViewModel?.ClearHistory();
}
