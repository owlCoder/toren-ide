using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Toren.App.AspNetCore.Services;
using Toren.App.AspNetCore.ViewModels;
using Toren.App.DataTools.Services;
using Toren.App.DataTools.ViewModels;
using Toren.App.Debugging.Services;
using Toren.App.Debugging.ViewModels;
using Toren.App.Diagnostics.Contracts;
using Toren.App.Diagnostics.Services;
using Toren.App.Documents.Adapters;
using Toren.App.Documents.Contracts;
using Toren.App.Editor.Services;
using Toren.App.EnvironmentDoctor.Services;
using Toren.App.EnvironmentDoctor.ViewModels;
using Toren.App.Execution.Services;
using Toren.App.Execution.ViewModels;
using Toren.App.Http.Services;
using Toren.App.Http.ViewModels;
using Toren.App.Packages.Services;
using Toren.App.Packages.ViewModels;
using Toren.App.Search.Contracts;
using Toren.App.Search.Services;
using Toren.App.Settings.Adapters;
using Toren.App.Terminal.Services;
using Toren.App.Terminal.ViewModels;
using Toren.App.Testing.Contracts;
using Toren.App.Testing.Services;
using Toren.App.Testing.ViewModels;
using Toren.App.ViewModels;
using Toren.App.Views;
using Toren.Containers.Contracts;
using Toren.Containers.Services;
using Toren.Core.Execution.Contracts;
using Toren.Core.Navigation.Contracts;
using Toren.Debugging.Adapters;
using Toren.Debugging.Contracts;
using Toren.Debugging.Services;
using Toren.DotNet.AspNetCore.Contracts;
using Toren.DotNet.AspNetCore.Services;
using Toren.DotNet.EntityFramework.Contracts;
using Toren.DotNet.EntityFramework.Services;
using Toren.DotNet.Environment.Contracts;
using Toren.DotNet.Environment.Services;
using Toren.DotNet.Execution.Adapters;
using Toren.DotNet.Execution.Contracts;
using Toren.DotNet.Execution.Services;
using Toren.DotNet.Http.Parsers;
using Toren.DotNet.Http.Services;
using Toren.DotNet.Packages.Contracts;
using Toren.DotNet.Packages.Services;
using Toren.DotNet.Testing.Contracts;
using Toren.DotNet.Testing.Services;
using Toren.DotNet.UserSecrets.Contracts;
using Toren.DotNet.UserSecrets.Services;
using Toren.Language.CSharp.Contracts;
using Toren.Language.CSharp.Models;
using Toren.Language.CSharp.Services;
using Toren.Platform.Execution.Adapters;
using Toren.Platform.Navigation.Adapters;
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
    private static readonly System.Net.Http.HttpClient SharedHttpClient = new();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = CreateMainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    internal static MainWindow CreateMainWindow(
        string? applicationDataDirectory = null,
        IInteractiveProcessRunner? terminalRunner = null,
        IDotNetCommandService? commandService = null)
    {
        var processRunner = new SystemProcessRunner();
        IInteractiveProcessRunner interactiveProcessRunner = terminalRunner ?? new SystemInteractiveProcessRunner();
        INativeShellProvider nativeShellProvider = new SystemNativeShellProvider();
        Toren.Git.Contracts.IGitRepositoryService gitRepositoryService =
            new Toren.Git.Services.GitRepositoryService(processRunner);
        Toren.Git.Contracts.IGitEnvironmentService gitEnvironmentService =
            new Toren.Git.Services.GitEnvironmentService(processRunner);
        IExternalUriLauncher externalUriLauncher = new SystemExternalUriLauncher();
        IDotNetEnvironmentService dotNetEnvironmentService = new DotNetEnvironmentService(processRunner);
        IDotNetSdkResolver dotNetSdkResolver = new DotNetSdkResolver(processRunner);
        IDotNetCommandService dotNetCommandService = commandService ?? new DotNetCommandService(processRunner);
        IDotNetLaunchProfileProvider dotNetLaunchProfileProvider = new FileDotNetLaunchProfileProvider();
        IDotNetPackageService dotNetPackageService = new DotNetPackageService(processRunner);
        IDotNetUserSecretsService dotNetUserSecretsService = new DotNetUserSecretsService(processRunner);
        IEfCoreToolService efCoreToolService = new EfCoreToolService(processRunner);
        IDockerComposeService dockerComposeService = new DockerComposeService(processRunner);
        IDockerComposeFileLocator dockerComposeFileLocator = new FileSystemDockerComposeFileLocator();
        IHttpsDevelopmentCertificateService httpsDevelopmentCertificateService =
            new HttpsDevelopmentCertificateService(processRunner);
        IAspNetApiShortcutResolver aspNetApiShortcutResolver = new AspNetApiShortcutResolver();
        IDotNetTestDiscoveryService dotNetTestDiscoveryService = new DotNetTestDiscoveryService(processRunner);
        IDotNetTestRunService dotNetTestRunService = new DotNetTestRunService(processRunner);
        IDebugSessionService debugSessionService = new DapDebugSessionService(
            new NetCoreDbgAdapterLocator(),
            new StdioDebugAdapterTransportFactory(),
            new DapDebugAdapterClientFactory());
        var debugSessionCoordinator = new DebugSessionCoordinator(debugSessionService);
        IDotNetTestDebugService dotNetTestDebugService = new DebuggerAttachingTestDebugService(
            new DotNetTestDebugService(processRunner),
            debugSessionCoordinator);
        IWorkspaceClassifier workspaceClassifier = new WorkspaceClassifier();
        ISolutionProjectProvider solutionProjectProvider = new DotNetSolutionProjectProvider(processRunner);
        IFolderProjectProvider folderProjectProvider = new FileSystemFolderProjectProvider();
        var evaluationRunner = new MsBuildEvaluationProcessRunner(processRunner);
        IProjectMetadataProvider projectMetadataProvider = new MsBuildProjectMetadataProvider(evaluationRunner);
        IProjectReferenceProvider projectReferenceProvider = new MsBuildProjectReferenceProvider(evaluationRunner);
        IProjectCompilationReferenceProvider projectCompilationReferenceProvider =
            new MsBuildProjectCompilationReferenceProvider(evaluationRunner);
        IWorkspaceTreeService workspaceTreeService = new WorkspaceTreeService(
            solutionProjectProvider,
            projectReferenceProvider);
        IWorkspaceProjectGraphService projectGraphService = new WorkspaceProjectGraphService(
            folderProjectProvider,
            solutionProjectProvider,
            projectMetadataProvider,
            projectReferenceProvider);
        IWorkspaceTestDiscoveryService workspaceTestDiscoveryService = new WorkspaceTestDiscoveryService(
            projectGraphService,
            dotNetTestDiscoveryService);
        var workspaceExecutionTargetService = new WorkspaceExecutionTargetService(projectGraphService);
        IWorkspaceFileProvider workspaceFileProvider = new FileSystemWorkspaceFileProvider();
        IWorkspaceFileSearchService workspaceFileSearchService = new WorkspaceFileSearchService();
        applicationDataDirectory ??= Path.Combine(
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
        IProblemsWorkspaceScopeService problemsWorkspaceScopeService = new WorkspaceProblemsScopeService(
            workspaceClassifier,
            projectGraphService,
            workspaceFileProvider);
        IDotNetCommandDiagnosticParser dotNetCommandDiagnosticParser = new DotNetCommandDiagnosticParser();
        ICSharpSyntaxService cSharpSyntaxService = new RoslynCSharpSyntaxService();
        var roslynDiagnosticService = new RoslynCSharpDiagnosticService();
        ICSharpDiagnosticService cSharpDiagnosticService = roslynDiagnosticService;
        ICSharpWorkspaceDiagnosticService cSharpWorkspaceDiagnosticService = roslynDiagnosticService;
        ICSharpCodeActionService cSharpCodeActionService = new RoslynCSharpCodeActionService();
        ICSharpSemanticService cSharpSemanticService = new RoslynCSharpSemanticService();
        ICSharpSemanticHighlightingService cSharpSemanticHighlightingService =
            new RoslynCSharpSemanticHighlightingService();
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
            textDocumentStore,
            projectCompilationReferenceProvider);
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
        var mainWindow = new MainWindow(
            viewModel,
            new FileApplicationSettingsStore(Path.Combine(applicationDataDirectory, "settings.json")),
            documentSessionStore);
        mainWindow.Closed += async (_, _) =>
            await debugSessionCoordinator.DisposeAsync().ConfigureAwait(true);
        var workspaceExecution = new WorkspaceExecutionViewModel(dotNetCommandService);
        WorkspaceExecutionController.Attach(
            mainWindow,
            viewModel,
            workspaceClassifier,
            workspaceExecutionTargetService,
            dotNetLaunchProfileProvider,
            workspaceExecution);
        var aspNetCoreToolsController = AspNetCoreToolsController.Attach(
            mainWindow,
            workspaceExecution,
            new AspNetCoreToolsViewModel(
                dotNetUserSecretsService,
                httpsDevelopmentCertificateService,
                aspNetApiShortcutResolver,
                externalUriLauncher));
        DataToolsController.Attach(
            mainWindow,
            viewModel,
            workspaceExecution,
            new DataToolsViewModel(
                efCoreToolService,
                dockerComposeService,
                dockerComposeFileLocator));
        EnvironmentDoctorController.Attach(
            mainWindow,
            viewModel,
            new EnvironmentDoctorViewModel(
                new EnvironmentDoctorService(
                    dotNetSdkResolver,
                    gitEnvironmentService,
                    dockerComposeService,
                    httpsDevelopmentCertificateService)));
        WorkspaceRunBrowserController.Attach(
            mainWindow,
            workspaceExecution,
            new AspNetLaunchUriResolver(),
            externalUriLauncher,
            viewModel.SetStatus,
            uri => aspNetCoreToolsController?.SetApplicationUri(uri));
        WorkspaceExecutionDiagnosticsController.Attach(
            mainWindow,
            workspaceExecution,
            dotNetCommandDiagnosticParser,
            viewModel);
        TestExplorerController.Attach(
            mainWindow,
            viewModel,
            workspaceClassifier,
            workspaceTestDiscoveryService,
            new TestExplorerViewModel(dotNetTestRunService, dotNetTestDebugService),
            viewModel.SetStatus);
        DebugSessionController.Attach(
            mainWindow,
            viewModel,
            debugSessionCoordinator,
            new DebugSessionViewModel(debugSessionCoordinator),
            viewModel.SetStatus);
        TerminalController.Attach(
            mainWindow,
            viewModel,
            new TerminalViewModel(interactiveProcessRunner, nativeShellProvider));
        Toren.App.SourceControl.Services.SourceControlController.Attach(
            mainWindow,
            viewModel,
            new Toren.App.SourceControl.ViewModels.SourceControlViewModel(gitRepositoryService),
            viewModel.SetStatus);
        PackageManagerController.Attach(
            mainWindow,
            viewModel,
            workspaceClassifier,
            projectGraphService,
            new PackageManagerViewModel(dotNetPackageService),
            viewModel.SetStatus);
        HttpClientController.Attach(
            mainWindow,
            viewModel,
            new HttpClientViewModel(
                new HttpRequestDocumentParser(),
                new HttpRequestVariableResolver(),
                new HttpRequestRunner(SharedHttpClient),
                new HttpResponseFormatter(),
                new HttpRequestHistory()));
        ProblemsScopeController.Attach(
            mainWindow,
            viewModel,
            problemsWorkspaceScopeService,
            viewModel.SetStatus);
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
        CSharpSemanticHighlightingController.Attach(
            mainWindow,
            cSharpSemanticHighlightingService,
            GetActiveCSharpDocument,
            GetActiveSemanticContextAsync,
            documentHost);
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
        return mainWindow;
    }

    private async void AboutToren_OnClick(object? sender, EventArgs eventArgs)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner })
        {
            await AboutWindow.ShowAsync(owner).ConfigureAwait(true);
        }
    }
}
