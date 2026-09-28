using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Toren.App.Diagnostics.Contracts;
using Toren.App.Diagnostics.Services;
using Toren.App.Documents.Adapters;
using Toren.App.Documents.Contracts;
using Toren.App.Editor.Services;
using Toren.App.Search.Contracts;
using Toren.App.Search.Services;
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
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

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
            IWorkspaceTextSearchService workspaceTextSearchService = new WorkspaceTextSearchService(
                workspaceFileProvider,
                textDocumentStore);
            IDocumentSessionStore documentSessionStore = new FileDocumentSessionStore(
                Path.Combine(applicationDataDirectory, "document-session.json"));
            IProblemsViewStateStore problemsViewStateStore =
                new Diagnostics.Adapters.FileProblemsViewStateStore(
                    Path.Combine(applicationDataDirectory, "problems-view-state.json"));
            ICSharpSyntaxService cSharpSyntaxService = new RoslynCSharpSyntaxService();
            var roslynDiagnosticService = new RoslynCSharpDiagnosticService();
            ICSharpDiagnosticService cSharpDiagnosticService = roslynDiagnosticService;
            ICSharpWorkspaceDiagnosticService cSharpWorkspaceDiagnosticService = roslynDiagnosticService;
            ICSharpCodeActionService cSharpCodeActionService = new RoslynCSharpCodeActionService();
            ICSharpSemanticService cSharpSemanticService = new RoslynCSharpSemanticService();
            ICSharpCompletionService cSharpCompletionService = new RoslynCSharpCompletionService();
            ICSharpFormattingService cSharpFormattingService = new RoslynCSharpFormattingService();
            ICSharpRenameService cSharpRenameService = new RoslynCSharpRenameService();
            ICSharpSymbolIndexService cSharpSymbolIndexService = new RoslynCSharpSymbolIndexService();
            ICSharpSymbolSearchService cSharpSymbolSearchService = new CSharpSymbolSearchService();
            var documentHost = new DocumentHostViewModel(textDocumentStore);
            var cSharpRenameChangeApplier = new CSharpRenameChangeApplier(documentHost, textDocumentStore);
            var cSharpSemanticContextProvider = new CSharpSemanticContextProvider(
                workspaceClassifier,
                projectGraphService,
                workspaceFileProvider,
                textDocumentStore);
            IWorkspaceDiagnosticsCoordinator workspaceDiagnosticsCoordinator = new WorkspaceDiagnosticsCoordinator(
                cSharpSemanticContextProvider,
                cSharpWorkspaceDiagnosticService,
                cSharpSyntaxService);
            MainWindowViewModel? viewModel = null;

            string? GetWorkspacePath() =>
                viewModel is { Explorer.IsWorkspaceOpen: true } ? viewModel.WorkspacePath : null;

            CSharpSourceDocument? GetActiveCSharpDocument()
            {
                var document = documentHost.ActiveDocument;
                return document is not null
                    && Path.GetExtension(document.Path).Equals(".cs", StringComparison.OrdinalIgnoreCase)
                        ? new CSharpSourceDocument(document.Path, document.Text)
                        : null;
            }

            IReadOnlyList<CSharpSourceDocument> GetOpenCSharpDocuments() =>
                documentHost.OpenDocuments
                    .Where(document => Path.GetExtension(document.Path).Equals(".cs", StringComparison.OrdinalIgnoreCase))
                    .Select(document => new CSharpSourceDocument(document.Path, document.Text))
                    .ToArray();

            IReadOnlyDictionary<string, string> GetOpenDocumentTextOverrides() =>
                documentHost.OpenDocuments.ToDictionary(
                    document => Path.GetFullPath(document.Path),
                    document => document.Text,
                    PathComparer);

            async Task<CSharpSemanticContext?> GetSemanticContextAsync(
                CSharpSourceDocument activeDocument,
                CancellationToken cancellationToken = default)
            {
                var workspacePath = GetWorkspacePath();
                if (string.IsNullOrWhiteSpace(workspacePath))
                {
                    return null;
                }

                return await cSharpSemanticContextProvider
                    .CreateAsync(workspacePath, activeDocument, GetOpenCSharpDocuments(), cancellationToken)
                    .ConfigureAwait(true);
            }

            Task<CSharpSemanticContext?> GetActiveSemanticContextAsync()
            {
                var activeDocument = GetActiveCSharpDocument();
                return activeDocument is null
                    ? Task.FromResult<CSharpSemanticContext?>(null)
                    : GetSemanticContextAsync(activeDocument);
            }

            async Task<CSharpSemanticContext?> GetWorkspaceSemanticContextAsync()
            {
                var workspacePath = GetWorkspacePath();
                if (string.IsNullOrWhiteSpace(workspacePath))
                {
                    return null;
                }

                return await cSharpSemanticContextProvider
                    .CreateWorkspaceAsync(workspacePath, GetOpenCSharpDocuments())
                    .ConfigureAwait(true);
            }

            Task<CSharpSemanticContext?> CreateDiagnosticsContextAsync(
                string path,
                string sourceText,
                CancellationToken cancellationToken)
            {
                var activeDocument = new CSharpSourceDocument(path, sourceText);
                return GetSemanticContextAsync(activeDocument, cancellationToken);
            }

            IDocumentDiagnosticsCoordinator documentDiagnosticsCoordinator =
                new DocumentDiagnosticsCoordinator(
                    cSharpDiagnosticService,
                    cSharpSyntaxService,
                    CreateDiagnosticsContextAsync);

            viewModel = new MainWindowViewModel(
                dotNetEnvironmentService,
                dotNetSdkResolver,
                workspaceClassifier,
                workspaceTreeService,
                recentWorkspaceStore,
                documentSessionStore,
                documentDiagnosticsCoordinator,
                documentHost,
                workspaceDiagnosticsCoordinator);
            var mainWindow = new MainWindow(viewModel);
            ProblemsViewStateController.Attach(
                mainWindow,
                viewModel.Problems,
                problemsViewStateStore,
                viewModel.SetStatus);

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
            CSharpQuickInfoController.Attach(
                mainWindow,
                cSharpSemanticService,
                GetActiveCSharpDocument,
                GetActiveSemanticContextAsync);
            CSharpCompletionController.Attach(
                mainWindow,
                cSharpCompletionService,
                GetActiveCSharpDocument,
                GetActiveSemanticContextAsync);
            CSharpCodeActionController.Attach(
                mainWindow,
                cSharpCodeActionService,
                GetActiveCSharpDocument,
                GetActiveSemanticContextAsync,
                viewModel.SetStatus);
            CSharpFormattingController.Attach(
                mainWindow,
                cSharpFormattingService,
                GetActiveCSharpDocument,
                viewModel.SetStatus);
            CSharpRenameController.Attach(
                mainWindow,
                cSharpSemanticService,
                cSharpRenameService,
                cSharpRenameChangeApplier,
                GetActiveCSharpDocument,
                GetActiveSemanticContextAsync,
                viewModel.SetStatus);
            CSharpSymbolNavigationController.Attach(
                mainWindow,
                cSharpSymbolIndexService,
                cSharpSymbolSearchService,
                GetWorkspaceSemanticContextAsync,
                OpenDocumentPathAsync,
                viewModel.SetStatus);
            WorkspaceQuickOpenController.Attach(
                mainWindow,
                workspaceFileProvider,
                workspaceFileSearchService,
                GetWorkspacePath,
                OpenDocumentPathAsync,
                viewModel.SetStatus);
            WorkspaceTextSearchController.Attach(
                mainWindow,
                workspaceTextSearchService,
                GetWorkspacePath,
                GetOpenDocumentTextOverrides,
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
