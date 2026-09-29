using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Toren.App.Testing.Models;
using Toren.App.Testing.ViewModels;
using Toren.DotNet.Testing.Models;

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

    private async void RerunFailedProjects_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.RerunFailedProjectsAsync().ConfigureAwait(true);
        }
    }

    private async void RunTest_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Button { DataContext: DotNetTestCase test } button
            || ViewModel is not { } viewModel)
        {
            return;
        }

        var project = button
            .GetVisualAncestors()
            .OfType<Control>()
            .Select(static control => control.DataContext)
            .OfType<WorkspaceTestProjectDiscovery>()
            .FirstOrDefault();
        if (project is null)
        {
            return;
        }

        await viewModel.RunTestAsync(project, test).ConfigureAwait(true);
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
