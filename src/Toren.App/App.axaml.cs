using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Toren.App.Documents.Adapters;
using Toren.App.Documents.Contracts;
using Toren.App.ViewModels;
using Toren.App.Views;
using Toren.Core.Execution.Contracts;
using Toren.DotNet.Environment.Contracts;
using Toren.DotNet.Environment.Services;
using Toren.Platform.Execution.Adapters;
using Toren.Workspaces.Adapters;
using Toren.Workspaces.Contracts;
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
            IDotNetSdkResolver dotNetSdkResolver = new DotNetSdkResolver(processRunner);
            IWorkspaceClassifier workspaceClassifier = new WorkspaceClassifier();
            ISolutionProjectProvider solutionProjectProvider = new DotNetSolutionProjectProvider(processRunner);
            IProjectReferenceProvider projectReferenceProvider = new MsBuildProjectReferenceProvider(processRunner);
            IWorkspaceTreeService workspaceTreeService = new WorkspaceTreeService(
                solutionProjectProvider,
                projectReferenceProvider);
            var historyPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TorenIDE",
                "recent-workspaces.json");
            IRecentWorkspaceStore recentWorkspaceStore = new FileRecentWorkspaceStore(historyPath);
            ITextDocumentStore textDocumentStore = new FileTextDocumentStore();
            var documentHost = new DocumentHostViewModel(textDocumentStore);

            var viewModel = new MainWindowViewModel(
                dotNetEnvironmentService,
                dotNetSdkResolver,
                workspaceClassifier,
                workspaceTreeService,
                recentWorkspaceStore,
                documentHost);
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
