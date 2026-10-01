using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Toren.App.Packages.ViewModels;

namespace Toren.App.Views.Packages;

internal sealed partial class PackageManagerPanel : UserControl
{
    public PackageManagerPanel()
    {
        InitializeComponent();
    }

    private PackageManagerViewModel? ViewModel => DataContext as PackageManagerViewModel;

    private async void Project_OnSelectionChanged(object? sender, SelectionChangedEventArgs eventArgs)
    {
        // Bindings stay live while the dialog is closed; only a panel on screen starts the
        // SDK query. The controller runs it when the dialog is opened.
        if (ViewModel is { } viewModel && TopLevel.GetTopLevel(this) is not null)
        {
            await viewModel.RefreshInstalledAsync().ConfigureAwait(true);
        }
    }

    private async void Search_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.SearchAsync().ConfigureAwait(true);
        }
    }

    private async void SearchBox_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.Enter && ViewModel is { CanSearch: true } viewModel)
        {
            eventArgs.Handled = true;
            await viewModel.SearchAsync().ConfigureAwait(true);
        }
    }

    private async void RefreshInstalled_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.RefreshInstalledAsync().ConfigureAwait(true);
        }
    }

    private async void Install_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.InstallSelectedAsync().ConfigureAwait(true);
        }
    }

    private async void Update_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.UpdateSelectedAsync().ConfigureAwait(true);
        }
    }

    private async void Remove_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.RemoveSelectedAsync().ConfigureAwait(true);
        }
    }
}
