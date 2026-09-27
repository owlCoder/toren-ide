using NUnit.Framework;
using Toren.App.Documents.Contracts;
using Toren.App.Documents.Models;
using Toren.App.ViewModels;
using Toren.Core.Results;
using Toren.DotNet.Environment.Contracts;
using Toren.DotNet.Environment.Models;
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

    private static MainWindowViewModel CreateViewModel(
        IWorkspaceTreeService tree,
        IRecentWorkspaceStore recent,
        IDotNetSdkResolver sdkResolver) =>
        new(
            new FakeDotNetEnvironmentService(),
            sdkResolver,
            new WorkspaceClassifier(),
            tree,
            recent,
            new DocumentHostViewModel(new FakeTextDocumentStore()));

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
                new TextDocumentContent(path, string.Empty, TextDocumentEncoding.Utf8)));

        public Task<Result<TextDocumentContent>> SaveAsync(
            TextDocumentContent document,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(document));
    }
}
