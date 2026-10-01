using NUnit.Framework;
using Toren.App.ViewModels;
using Toren.Core.Results;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Models;

namespace Toren.UnitTests.Workspaces;

[TestFixture]
public sealed class ExplorerPopulationTests
{
    private static readonly WorkspaceDescriptor Workspace = new("/work/Large.sln", "Large", WorkspaceKind.Solution);

    [Test]
    public async Task LargeNodeIsPopulatedInStepsThatHandControlBackToTheUserInterface()
    {
        var yields = new List<(int ChildrenSoFar, bool Loaded)>();
        WorkspaceNodeViewModel? root = null;
        var explorer = new ExplorerViewModel(new FixedTreeService(40), () =>
        {
            yields.Add((root!.Children.Count, root.IsLoaded));
            return Task.CompletedTask;
        });
        explorer.Open(Workspace);
        root = explorer.Roots[0];

        var result = await explorer.ExpandAsync(root);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(root.Children.Select(child => child.Name),
                Is.EqualTo(Enumerable.Range(0, 40).Select(index => $"Project{index:00}")));
            Assert.That(root.IsLoaded, Is.True);
            Assert.That(string.Join(",", yields.Select(step => step.ChildrenSoFar)), Is.EqualTo("8,16,24,32"));
            Assert.That(yields.Select(step => step.Loaded), Is.All.False);
        });
    }

    [Test]
    public async Task SmallNodeIsPopulatedAtOnce()
    {
        var yields = 0;
        var explorer = new ExplorerViewModel(new FixedTreeService(8), () =>
        {
            yields++;
            return Task.CompletedTask;
        });
        explorer.Open(Workspace);

        await explorer.ExpandAsync(explorer.Roots[0]);

        Assert.Multiple(() =>
        {
            Assert.That(explorer.Roots[0].Children, Has.Count.EqualTo(8));
            Assert.That(yields, Is.Zero);
        });
    }

    [Test]
    public async Task ExpandingAgainWhileANodeIsBeingPopulatedDoesNotStartOver()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new FixedTreeService(20);
        var explorer = new ExplorerViewModel(service, () => release.Task);
        explorer.Open(Workspace);
        var root = explorer.Roots[0];

        var first = explorer.ExpandAsync(root);
        var second = await explorer.ExpandAsync(root);
        var partial = root.Children.Count;
        release.SetResult();
        await first;

        Assert.Multiple(() =>
        {
            Assert.That(second.IsSuccess, Is.True);
            Assert.That(partial, Is.EqualTo(8));
            Assert.That(root.Children, Has.Count.EqualTo(20));
            Assert.That(service.Requests, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task WithoutAUserInterfaceToYieldToAllNodesAreAddedDirectly()
    {
        var explorer = new ExplorerViewModel(new FixedTreeService(40));
        explorer.Open(Workspace);

        await explorer.ExpandAsync(explorer.Roots[0]);

        Assert.That(explorer.Roots[0].Children, Has.Count.EqualTo(40));
    }

    private sealed class FixedTreeService(int children) : IWorkspaceTreeService
    {
        public int Requests { get; private set; }

        public Result<WorkspaceNode> CreateRoot(WorkspaceDescriptor workspace) =>
            Result.Success(new WorkspaceNode(workspace.Path, workspace.DisplayName, WorkspaceNodeKind.Solution));

        public Task<Result<IReadOnlyList<WorkspaceNode>>> GetChildrenAsync(
            WorkspaceNode node,
            CancellationToken cancellationToken = default)
        {
            Requests++;
            return Task.FromResult(Result.Success<IReadOnlyList<WorkspaceNode>>(Enumerable.Range(0, children)
                .Select(index => new WorkspaceNode($"/work/Project{index:00}/Project{index:00}.csproj", $"Project{index:00}", WorkspaceNodeKind.Project))
                .ToArray()));
        }
    }
}
