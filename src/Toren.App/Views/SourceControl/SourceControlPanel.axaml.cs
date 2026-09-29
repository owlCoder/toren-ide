using Avalonia.Controls;
using Avalonia.Interactivity;
using Toren.App.SourceControl.ViewModels;

namespace Toren.App.Views.SourceControl;

internal sealed partial class SourceControlPanel : UserControl
{
    public SourceControlPanel()
    {
        InitializeComponent();
    }

    private SourceControlViewModel? ViewModel => DataContext as SourceControlViewModel;

    private async void Refresh_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.RefreshAsync().ConfigureAwait(true);
        }
    }

    private async void Stage_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.StageSelectedAsync().ConfigureAwait(true);
        }
    }

    private async void Unstage_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.UnstageSelectedAsync().ConfigureAwait(true);
        }
    }

    private async void Commit_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.CommitAsync().ConfigureAwait(true);
        }
    }

    private async void Changes_OnSelectionChanged(object? sender, SelectionChangedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.LoadSelectedDiffAsync().ConfigureAwait(true);
        }
    }
}
