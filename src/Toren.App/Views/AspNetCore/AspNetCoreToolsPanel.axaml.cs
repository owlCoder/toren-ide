using Avalonia.Controls;
using Avalonia.Interactivity;
using Toren.App.AspNetCore.ViewModels;
using Toren.DotNet.AspNetCore.Models;

namespace Toren.App.Views.AspNetCore;

internal sealed partial class AspNetCoreToolsPanel : UserControl
{
    public AspNetCoreToolsPanel()
    {
        InitializeComponent();
    }

    private AspNetCoreToolsViewModel? ViewModel => DataContext as AspNetCoreToolsViewModel;

    private async void Refresh_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.RefreshAsync().ConfigureAwait(true);
        }
    }

    private async void InitializeSecrets_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.InitializeSecretsAsync().ConfigureAwait(true);
        }
    }

    private async void SaveSecret_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.SaveSecretAsync().ConfigureAwait(true);
        }
    }

    private async void RemoveSecret_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.RemoveSelectedSecretAsync().ConfigureAwait(true);
        }
    }

    private async void ClearSecrets_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.ClearSecretsAsync().ConfigureAwait(true);
        }
    }

    private async void TrustHttps_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.TrustHttpsAsync().ConfigureAwait(true);
        }
    }

    private void OpenShortcut_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel && sender is Button { DataContext: AspNetApiShortcut shortcut })
        {
            viewModel.OpenShortcut(shortcut);
        }
    }
}
