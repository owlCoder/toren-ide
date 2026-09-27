using NUnit.Framework;
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
    public async Task OpeningSolutionLoadsFirstLevelWithoutTreeViewEvent()
    {
        var workspacePath = Path.Combine(Path.GetTempPath(), "ParcelBox.sln");
        var tree = new FakeWorkspaceTreeService();
        var viewModel = CreateViewModel(tree, new FakeRecentWorkspaceStore([]));

        await viewModel.OpenWorkspaceFileAsync(workspacePath);

        Assert.Multiple(() =>
        {
            Assert.That(tree.ChildLoadCount, Is.EqualTo(1));
            Assert.That(viewModel.Explorer.Roots, Has.Count.EqualTo(1));
            Assert.That(viewModel.Explorer.Roots[0].IsExpanded, Is.True);
            Assert.That(viewModel.Explorer.Roots[0].Children[0].Name, Is.EqualTo("ParcelBox.Api.csproj"));
        });
    }

    [Test]
    public async Task RestoringSolutionLoadsFirstLevelAndKeepsWelcomeAvailable()
    {
        var workspace = new WorkspaceDescriptor(
            Path.Combine(Path.GetTempPath(), "ParcelBox.sln"), "ParcelBox", WorkspaceKind.Solution);
        var tree = new FakeWorkspaceTreeService();
        var viewModel = CreateViewModel(tree, new FakeRecentWorkspaceStore([workspace]));

        await viewModel.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(tree.ChildLoadCount, Is.EqualTo(1));
            Assert.That(viewModel.Explorer.Roots[0].Children[0].Name, Is.EqualTo("ParcelBox.Api.csproj"));
            Assert.That(viewModel.IsWelcomeOpen, Is.True);
        });
    }

    private static MainWindowViewModel CreateViewModel(
        IWorkspaceTreeService tree,
        IRecentWorkspaceStore recent) =>
        new(new FakeDotNetEnvironmentService(), new WorkspaceClassifier(), tree, recent);

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
}
