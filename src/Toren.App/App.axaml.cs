using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Toren.App.Diagnostics.Contracts;
using Toren.App.Diagnostics.Services;
using Toren.App.Documents.Adapters;
using Toren.App.Documents.Contracts;
using Toren.App.Editor.Services;
using Toren.App.ViewModels;
using Toren.App.Views;
using Toren.Core.Execution.Contracts;
using Toren.DotNet.Environment.Contracts;
using Toren.DotNet.Environment.Services;
using Toren.Language.CSharp.Contracts;
using Toren.Language.CSharp.Services;
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
            var applicationDataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TorenIDE");
            IRecentWorkspaceStore recentWorkspaceStore = new FileRecentWorkspaceStore(
                Path.Combine(applicationDataDirectory, "recent-workspaces.json"));
            ITextDocumentStore textDocumentStore = new FileTextDocumentStore();
            IDocumentSessionStore documentSessionStore = new FileDocumentSessionStore(
                Path.Combine(applicationDataDirectory, "document-session.json"));
            ICSharpSyntaxService cSharpSyntaxService = new RoslynCSharpSyntaxService();
            ICSharpSemanticService cSharpSemanticService = new RoslynCSharpSemanticService();
            IDocumentDiagnosticsCoordinator documentDiagnosticsCoordinator =
                new DocumentDiagnosticsCoordinator(cSharpSyntaxService);
            var documentHost = new DocumentHostViewModel(textDocumentStore);

            var viewModel = new MainWindowViewModel(
                dotNetEnvironmentService,
                dotNetSdkResolver,
                workspaceClassifier,
                workspaceTreeService,
                recentWorkspaceStore,
                documentSessionStore,
                documentDiagnosticsCoordinator,
                documentHost);
            var mainWindow = new MainWindow(viewModel);
            EditorNavigationController.Attach(mainWindow, cSharpSemanticService);
            desktop.MainWindow = mainWindow;
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
