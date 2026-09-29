using Avalonia.Controls;
using Avalonia.Interactivity;
using Toren.App.Testing.ViewModels;

namespace Toren.App.Views.Testing;

internal sealed partial class TestExplorerPanel : UserControl
{
    public TestExplorerPanel()
    {
        InitializeComponent();
    }

    private TestExplorerViewModel? ViewModel => DataContext as TestExplorerViewModel;

    private async void RunAll_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.RunAllAsync().ConfigureAwait(true);
        }
    }

    private void Stop_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        ViewModel?.Stop();
    }

    private void ClearOutput_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        ViewModel?.ClearOutput();
    }
}
