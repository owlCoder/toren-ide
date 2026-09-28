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
using Toren.Language.CSharp.Models;
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
            IFolderProjectProvider folderProjectProvider = new FileSystemFolderProjectProvider();
            IProjectMetadataProvider projectMetadataProvider = new MsBuildProjectMetadataProvider(processRunner);
            IProjectReferenceProvider projectReferenceProvider = new MsBuildProjectReferenceProvider(processRunner);
            IWorkspaceTreeService workspaceTreeService = new WorkspaceTreeService(
                solutionProjectProvider,
                projectReferenceProvider);
            IWorkspaceProjectGraphService projectGraphService = new WorkspaceProjectGraphService(
                folderProjectProvider,
                solutionProjectProvider,
                projectMetadataProvider,
                projectReferenceProvider);
            IWorkspaceFileProvider workspaceFileProvider = new FileSystemWorkspaceFileProvider();
            IWorkspaceFileSearchService workspaceFileSearchService = new WorkspaceFileSearchService();
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
            ICSharpSymbolIndexService cSharpSymbolIndexService = new RoslynCSharpSymbolIndexService();
            ICSharpSymbolSearchService cSharpSymbolSearchService = new CSharpSymbolSearchService();
            IDocumentDiagnosticsCoordinator documentDiagnosticsCoordinator =
                new DocumentDiagnosticsCoordinator(cSharpSyntaxService);
            var documentHost = new DocumentHostViewModel(textDocumentStore);
            var cSharpSemanticContextProvider = new CSharpSemanticContextProvider(
                workspaceClassifier,
                projectGraphService,
                workspaceFileProvider,
                textDocumentStore);

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

            string? GetWorkspacePath() =>
                viewModel.Explorer.IsWorkspaceOpen ? viewModel.WorkspacePath : null;

            CSharpSourceDocument? GetActiveCSharpDocument()
            {
                var document = viewModel.Documents.ActiveDocument;
                return document is not null
                    && Path.GetExtension(document.Path).Equals(".cs", StringComparison.OrdinalIgnoreCase)
                        ? new CSharpSourceDocument(document.Path, document.Text)
                        : null;
            }

            IReadOnlyList<CSharpSourceDocument> GetOpenCSharpDocuments() =>
                viewModel.Documents.OpenDocuments
                    .Where(document => Path.GetExtension(document.Path).Equals(".cs", StringComparison.OrdinalIgnoreCase))
                    .Select(document => new CSharpSourceDocument(document.Path, document.Text))
                    .ToArray();

            async Task<CSharpSemanticContext?> GetActiveSemanticContextAsync()
            {
                var workspacePath = GetWorkspacePath();
                var activeDocument = GetActiveCSharpDocument();
                if (string.IsNullOrWhiteSpace(workspacePath) || activeDocument is null)
                {
                    return null;
                }

                return await cSharpSemanticContextProvider
                    .CreateAsync(workspacePath, activeDocument, GetOpenCSharpDocuments())
                    .ConfigureAwait(true);
            }

            async Task OpenDocumentPathAsync(string path)
            {
                var opened = await viewModel.Documents.OpenAsync(path).ConfigureAwait(true);
                if (!opened.IsSuccess)
                {
                    viewModel.SetStatus(opened.Error.Message);
                    return;
                }

                await viewModel.ActivateDocumentAsync(opened.Value).ConfigureAwait(true);
                viewModel.SetStatus($"Opened {opened.Value.Title}");
            }

            EditorSearchController.Attach(mainWindow);
            CSharpNavigationController.Attach(
                mainWindow,
                cSharpSemanticService,
                GetActiveCSharpDocument,
                GetActiveSemanticContextAsync,
                OpenDocumentPathAsync);
            CSharpSymbolNavigationController.Attach(
                mainWindow,
                cSharpSymbolIndexService,
                cSharpSymbolSearchService,
                GetActiveSemanticContextAsync,
                OpenDocumentPathAsync,
                viewModel.SetStatus);
            WorkspaceQuickOpenController.Attach(
                mainWindow,
                workspaceFileProvider,
                workspaceFileSearchService,
                GetWorkspacePath,
                OpenDocumentPathAsync,
                viewModel.SetStatus);
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
