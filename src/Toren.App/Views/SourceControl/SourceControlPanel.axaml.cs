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

    private async void SwitchBranch_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.SwitchSelectedBranchAsync().ConfigureAwait(true);
        }
    }

    private async void CreateBranch_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.CreateBranchAsync().ConfigureAwait(true);
        }
    }

    private async void Fetch_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.FetchAsync().ConfigureAwait(true);
        }
    }

    private async void Pull_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.PullAsync().ConfigureAwait(true);
        }
    }

    private async void Push_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.PushAsync().ConfigureAwait(true);
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
