using NUnit.Framework;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Toren.App.Diagnostics.Contracts;
using Toren.App.Diagnostics.Models;
using Toren.App.Execution.ViewModels;
using Toren.DotNet.Execution.Contracts;
using Toren.DotNet.Execution.Models;
using Toren.App.Diagnostics.Services;
using Toren.App.Documents.Contracts;
using Toren.App.Documents.Models;
using Toren.App.ViewModels;
using Toren.Core.Results;
using Toren.DotNet.Environment.Contracts;
using Toren.DotNet.Environment.Models;
using Toren.Language.CSharp.Contracts;
using Toren.Language.CSharp.Models;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Models;
using Toren.Workspaces.Services;

namespace Toren.UnitTests.Workspaces;

[TestFixture]
public sealed class ExplorerOpeningTests
{
    [Test]
    public async Task OpeningSolutionLoadsFirstLevelAndResolvesWorkspaceSdk()
    {
        var workspacePath = Path.Combine(Path.GetTempPath(), "ParcelBox.sln");
        var tree = new FakeWorkspaceTreeService();
        var sdkResolver = new FakeDotNetSdkResolver();
        var viewModel = CreateViewModel(tree, new FakeRecentWorkspaceStore([]), sdkResolver);

        await viewModel.OpenWorkspaceFileAsync(workspacePath);

        Assert.Multiple(() =>
        {
            Assert.That(tree.ChildLoadCount, Is.EqualTo(1));
            Assert.That(viewModel.Explorer.Roots, Has.Count.EqualTo(1));
            Assert.That(viewModel.Explorer.Roots[0].IsExpanded, Is.True);
            Assert.That(viewModel.Explorer.Roots[0].Children[0].Name, Is.EqualTo("ParcelBox.Api.csproj"));
            Assert.That(viewModel.SdkSummary, Is.EqualTo(".NET SDK: 10.0.200"));
            Assert.That(
                sdkResolver.LastWorkingDirectory,
                Is.EqualTo(Path.GetDirectoryName(Path.GetFullPath(workspacePath))));
        });
    }

    [Test]
    public async Task RestoringSolutionLoadsFirstLevelAndKeepsWelcomeAvailable()
    {
        var workspace = new WorkspaceDescriptor(
            Path.Combine(Path.GetTempPath(), "ParcelBox.sln"), "ParcelBox", WorkspaceKind.Solution);
        var tree = new FakeWorkspaceTreeService();
        var sdkResolver = new FakeDotNetSdkResolver();
        var viewModel = CreateViewModel(tree, new FakeRecentWorkspaceStore([workspace]), sdkResolver);

        await viewModel.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(tree.ChildLoadCount, Is.EqualTo(1));
            Assert.That(viewModel.Explorer.Roots[0].Children[0].Name, Is.EqualTo("ParcelBox.Api.csproj"));
            Assert.That(viewModel.IsWelcomeOpen, Is.True);
            Assert.That(viewModel.SdkSummary, Is.EqualTo(".NET SDK: 10.0.200"));
        });
    }

    [Test]
    public async Task InitializeRestoresDocumentSessionAndActiveTab()
    {
        var firstPath = Path.Combine(Path.GetTempPath(), "Program.cs");
        var secondPath = Path.Combine(Path.GetTempPath(), "Settings.json");
        var session = new FakeDocumentSessionStore(
            new DocumentSessionState([firstPath, secondPath], firstPath));
        var viewModel = CreateViewModel(
            new FakeWorkspaceTreeService(),
            new FakeRecentWorkspaceStore([]),
            new FakeDotNetSdkResolver(),
            session);

        await viewModel.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Documents.OpenDocuments, Has.Count.EqualTo(2));
            Assert.That(viewModel.Documents.ActiveDocument?.Path, Is.EqualTo(Path.GetFullPath(firstPath)));
            Assert.That(viewModel.IsWelcomeSelected, Is.False);
        });
    }

    private static MainWindowViewModel CreateViewModel(
        IWorkspaceTreeService tree,
        IRecentWorkspaceStore recent,
        IDotNetSdkResolver sdkResolver,
        IDocumentSessionStore? documentSessionStore = null,
        IWorkspaceDiagnosticsCoordinator? workspaceDiagnostics = null)
    {
        var syntaxService = new FakeCSharpSyntaxService();
        return new MainWindowViewModel(
            new FakeDotNetEnvironmentService(),
            sdkResolver,
            new WorkspaceClassifier(),
            tree,
            recent,
            documentSessionStore ?? new FakeDocumentSessionStore(new DocumentSessionState([], null)),
            new DocumentDiagnosticsCoordinator(syntaxService, TimeSpan.Zero),
            new DocumentHostViewModel(new FakeTextDocumentStore()),
            workspaceDiagnostics);
    }

    [AvaloniaTest]
    [TestCase(DotNetCommandKind.Restore)]
    [TestCase(DotNetCommandKind.Build)]
    [TestCase(DotNetCommandKind.Rebuild)]
    [TestCase(DotNetCommandKind.Clean)]
    [TestCase(DotNetCommandKind.Publish)]
    public async Task WorkspaceCommandsReplaceStaleLanguageDiagnostics(DotNetCommandKind kind)
    {
        var coordinator = new EmptyWorkspaceDiagnostics();
        using var shell = CreateViewModel(new FakeWorkspaceTreeService(), new FakeRecentWorkspaceStore([]),
            new FakeDotNetSdkResolver(), workspaceDiagnostics: coordinator);
        var path = Path.Combine(Path.GetTempPath(), "ParcelBox.sln");
        shell.WorkspacePath = path;
        shell.Explorer.IsWorkspaceOpen = true;
        shell.Problems.Replace(path + ".cs", [new CSharpDiagnostic("CS0246", "Old missing reference",
            CSharpDiagnosticSeverity.Error, 1, 1, 1, 2)]);
        using var execution = new WorkspaceExecutionViewModel(new SuccessfulCommandService());
        execution.SetWorkspace(new WorkspaceDescriptor(path, "ParcelBox", WorkspaceKind.Solution));
        var window = new Window();
        window.Show();
        try
        {
            WorkspaceExecutionDiagnosticsController.Attach(window, execution, new DotNetCommandDiagnosticParser(), shell);
            await execution.ExecuteAsync(kind);
            Assert.That(coordinator.CallCount, Is.EqualTo(1));
            Assert.That(shell.Problems.HasAnyProblems, Is.False);
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task CompletionFromPreviousWorkspaceCannotReplaceCurrentProblems()
    {
        var coordinator = new EmptyWorkspaceDiagnostics();
        using var shell = CreateViewModel(new FakeWorkspaceTreeService(), new FakeRecentWorkspaceStore([]),
            new FakeDotNetSdkResolver(), workspaceDiagnostics: coordinator);
        shell.WorkspacePath = Path.Combine(Path.GetTempPath(), "Current.sln");
        shell.Explorer.IsWorkspaceOpen = true;
        shell.Problems.Replace(shell.WorkspacePath + ".cs", [new CSharpDiagnostic("CS0103", "Current error",
            CSharpDiagnosticSeverity.Error, 1, 1, 1, 2)]);
        using var execution = new WorkspaceExecutionViewModel(new SuccessfulCommandService());
        execution.SetWorkspace(new WorkspaceDescriptor(Path.Combine(Path.GetTempPath(), "Previous.sln"),
            "Previous", WorkspaceKind.Solution));
        var window = new Window();
        window.Show();
        try
        {
            WorkspaceExecutionDiagnosticsController.Attach(window, execution, new DotNetCommandDiagnosticParser(), shell);
            await execution.ExecuteAsync(DotNetCommandKind.Build);
            Assert.That(coordinator.CallCount, Is.Zero);
            Assert.That(shell.Problems.Items.Single().Code, Is.EqualTo("CS0103"));
        }
        finally { window.Close(); }
    }

    private sealed class EmptyWorkspaceDiagnostics : IWorkspaceDiagnosticsCoordinator
    {
        public int CallCount { get; private set; }
        public Task<WorkspaceDiagnosticsSnapshot?> AnalyzeLatestAsync(string workspacePath,
            IReadOnlyList<CSharpSourceDocument> openDocuments, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult<WorkspaceDiagnosticsSnapshot?>(new WorkspaceDiagnosticsSnapshot([], []));
        }
        public void CancelPending() { }
        public void Dispose() { }
    }

    private sealed class SuccessfulCommandService : IDotNetCommandService
    {
        public Task<Result<DotNetCommandResult>> ExecuteAsync(DotNetCommandRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(new DotNetCommandResult(request.Kind, 0, "Build succeeded", "")));
    }

    private sealed class FakeWorkspaceTreeService : IWorkspaceTreeService
    {
        public int ChildLoadCount { get; private set; }

        public Result<WorkspaceNode> CreateRoot(WorkspaceDescriptor workspace) =>
            Result.Success(new WorkspaceNode(workspace.Path, workspace.DisplayName, WorkspaceNodeKind.Solution));

        public Task<Result<IReadOnlyList<WorkspaceNode>>> GetChildrenAsync(
            WorkspaceNode node,
            CancellationToken cancellationToken = default)
        {
            ChildLoadCount++;
            IReadOnlyList<WorkspaceNode> children =
                [new WorkspaceNode("ParcelBox.Api.csproj", "ParcelBox.Api.csproj", WorkspaceNodeKind.Project)];
            return Task.FromResult(Result.Success(children));
        }
    }

    private sealed class FakeRecentWorkspaceStore(IReadOnlyList<WorkspaceDescriptor> workspaces) : IRecentWorkspaceStore
    {
        public Task<Result<IReadOnlyList<WorkspaceDescriptor>>> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(workspaces));

        public Task<Result<IReadOnlyList<WorkspaceDescriptor>>> RecordAsync(
            WorkspaceDescriptor workspace,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success<IReadOnlyList<WorkspaceDescriptor>>([workspace]));
    }

    private sealed class FakeDotNetEnvironmentService : IDotNetEnvironmentService
    {
        public Task<Result<IReadOnlyList<DotNetSdkInfo>>> GetInstalledSdksAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success<IReadOnlyList<DotNetSdkInfo>>([]));
    }

    private sealed class FakeDotNetSdkResolver : IDotNetSdkResolver
    {
        public string? LastWorkingDirectory { get; private set; }

        public Task<Result<string>> ResolveVersionAsync(
            string workingDirectory,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastWorkingDirectory = workingDirectory;
            return Task.FromResult(Result.Success("10.0.200"));
        }
    }

    private sealed class FakeTextDocumentStore : ITextDocumentStore
    {
        public Task<Result<TextDocumentContent>> LoadAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(
                new TextDocumentContent(Path.GetFullPath(path), string.Empty, TextDocumentEncoding.Utf8)));

        public Task<Result<TextDocumentContent>> SaveAsync(
            TextDocumentContent document,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(document));
    }

    private sealed class FakeDocumentSessionStore(DocumentSessionState session) : IDocumentSessionStore
    {
        public Task<Result<DocumentSessionState>> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(session));

        public Task<Result<DocumentSessionState>> SaveAsync(
            DocumentSessionState state,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(state));
    }

    private sealed class FakeCSharpSyntaxService : ICSharpSyntaxService
    {
        public Task<IReadOnlyList<CSharpDiagnostic>> AnalyzeAsync(
            string sourceText,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CSharpDiagnostic>>([]);
    }
}
