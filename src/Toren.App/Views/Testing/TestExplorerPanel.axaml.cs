using Avalonia.Controls;
using Avalonia.Input;
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

    internal Func<TestRunOutputLineViewModel, Task>? NavigateOutputAsync { get; set; }

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
        if (!TryGetTestContext(sender, out var project, out var test)
            || ViewModel is not { } viewModel)
        {
            return;
        }

        await viewModel.RunTestAsync(project, test).ConfigureAwait(true);
    }

    private async void DebugTest_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (!TryGetTestContext(sender, out var project, out var test)
            || ViewModel is not { } viewModel)
        {
            return;
        }

        await viewModel.DebugTestAsync(project, test).ConfigureAwait(true);
    }

    private async void TestOutputList_OnDoubleTapped(object? sender, TappedEventArgs eventArgs)
    {
        if (sender is ListBox { SelectedItem: TestRunOutputLineViewModel { CanNavigate: true } line }
            && NavigateOutputAsync is { } navigateOutputAsync)
        {
            await navigateOutputAsync(line).ConfigureAwait(true);
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

    private static bool TryGetTestContext(
        object? sender,
        out WorkspaceTestProjectDiscovery project,
        out DotNetTestCase test)
    {
        project = null!;
        test = null!;
        if (sender is not Button { DataContext: DotNetTestCase selectedTest } button)
        {
            return false;
        }

        var selectedProject = button
            .GetVisualAncestors()
            .OfType<Control>()
            .Select(static control => control.DataContext)
            .OfType<WorkspaceTestProjectDiscovery>()
            .FirstOrDefault();
        if (selectedProject is null)
        {
            return false;
        }

        project = selectedProject;
        test = selectedTest;
        return true;
    }
}
