using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Toren.App.ViewModels;
using Toren.App.Views;
using Toren.Core.Execution.Contracts;
using Toren.DotNet.Environment.Contracts;
using Toren.DotNet.Environment.Services;
using Toren.Platform.Execution.Adapters;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Adapters;
using Toren.Workspaces.Services;

namespace Toren.App;

public sealed partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            IProcessRunner processRunner = new SystemProcessRunner();
            IDotNetEnvironmentService dotNetEnvironmentService = new DotNetEnvironmentService(processRunner);
            IWorkspaceClassifier workspaceClassifier = new WorkspaceClassifier();
            IWorkspaceTreeService workspaceTreeService = new WorkspaceTreeService(processRunner);
            var historyPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TorenIDE",
                "recent-workspaces.json");
            IRecentWorkspaceStore recentWorkspaceStore = new FileRecentWorkspaceStore(historyPath);

            var viewModel = new MainWindowViewModel(
                dotNetEnvironmentService,
                workspaceClassifier,
                workspaceTreeService,
                recentWorkspaceStore);
            desktop.MainWindow = new MainWindow(viewModel);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async void AboutToren_OnClick(object? sender, EventArgs eventArgs)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner })
        {
            await AboutWindow.ShowAsync(owner).ConfigureAwait(true);
        }
    }
}
