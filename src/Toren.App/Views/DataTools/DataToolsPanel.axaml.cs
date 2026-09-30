using Avalonia.Controls;
using Avalonia.Interactivity;
using Toren.App.DataTools.ViewModels;

namespace Toren.App.Views.DataTools;

internal sealed partial class DataToolsPanel : UserControl
{
    public DataToolsPanel()
    {
        InitializeComponent();
    }

    private DataToolsViewModel? ViewModel => DataContext as DataToolsViewModel;

    private async void RefreshEf_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.RefreshEfAsync().ConfigureAwait(true);
        }
    }

    private async void AddMigration_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.AddMigrationAsync().ConfigureAwait(true);
        }
    }

    private async void RemoveMigration_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.RemoveMigrationAsync().ConfigureAwait(true);
        }
    }

    private async void UpdateDatabase_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.UpdateDatabaseAsync().ConfigureAwait(true);
        }
    }

    private async void RefreshDocker_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.RefreshDockerAsync().ConfigureAwait(true);
        }
    }

    private async void Up_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.UpAsync().ConfigureAwait(true);
        }
    }

    private async void Down_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.DownAsync().ConfigureAwait(true);
        }
    }

    private async void Build_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.BuildAsync().ConfigureAwait(true);
        }
    }

    private async void Logs_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.RefreshLogsAsync().ConfigureAwait(true);
        }
    }
}
