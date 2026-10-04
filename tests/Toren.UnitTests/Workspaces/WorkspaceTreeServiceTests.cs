using NUnit.Framework;
using Toren.Core.Results;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Models;
using Toren.Workspaces.Services;

namespace Toren.UnitTests.Workspaces;

[TestFixture]
public sealed class WorkspaceTreeServiceTests
{
    [Test]
    public void CreateRootRejectsMissingPath()
    {
        var service = CreateService();
        var missing = Path.Combine(Path.GetTempPath(), $"toren-missing-{Guid.NewGuid():N}");

        var result = service.CreateRoot(new WorkspaceDescriptor(missing, "Missing", WorkspaceKind.Folder));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("workspace.path.unavailable"));
        });
    }

    [Test]
    public async Task FolderChildrenContainStandardFilesAndHideBuildOutput()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "src"));
            Directory.CreateDirectory(Path.Combine(root, "bin"));
            File.WriteAllText(Path.Combine(root, ".editorconfig"), "root = true");
            File.WriteAllText(Path.Combine(root, "Sample.csproj"), "<Project />");

            var result = await CreateService()
                .GetChildrenAsync(new WorkspaceNode(root, "Sample", WorkspaceNodeKind.Folder));

            Assert.That(result.IsSuccess, Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(string.Join(",", result.Value!.Select(node => node.Name)), Is.EqualTo("src,.editorconfig,Sample.csproj"));
                Assert.That(result.Value!.Single(node => node.Name == "Sample.csproj").Kind, Is.EqualTo(WorkspaceNodeKind.Project));
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task SolutionChildrenUseShortestUniqueProjectNames()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var solution = Path.Combine(root, "ParcelBox.sln");
            File.WriteAllText(solution, string.Empty);
            var projects = new[]
            {
                Path.Combine(root, "src", "ParcelBox.Simulators.LockerControl", "ParcelBox.Simulators.LockerControl.csproj"),
                Path.Combine(root, "src", "ParcelBox.Simulators.MessageGateway", "ParcelBox.Simulators.MessageGateway.csproj"),
                Path.Combine(root, "src", "ParcelBox.Api", "ParcelBox.Api.csproj"),
            };
            var solutionProvider = new FakeSolutionProjectProvider(projects);
            var service = new WorkspaceTreeService(solutionProvider, new FakeProjectReferenceProvider([]));

            var result = await service.GetChildrenAsync(new WorkspaceNode(solution, "ParcelBox", WorkspaceNodeKind.Solution));

            Assert.That(result.IsSuccess, Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(string.Join(",", result.Value!.Select(node => node.Name)), Is.EqualTo("LockerControl,MessageGateway,Api"));
                Assert.That(result.Value!.Select(node => node.Path), Is.EqualTo(projects));
                Assert.That(result.Value!.All(node => node.Kind == WorkspaceNodeKind.Project), Is.True);
                Assert.That(solutionProvider.LastSolutionPath, Is.EqualTo(solution));
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task ProjectChildrenExposeReferencesAndPhysicalFiles()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var project = Path.Combine(root, "App.csproj");
            File.WriteAllText(project, "<Project />");
            File.WriteAllText(Path.Combine(root, "Program.cs"), "class Program {}");

            var children = await CreateService()
                .GetChildrenAsync(new WorkspaceNode(project, "App", WorkspaceNodeKind.Project));

            Assert.That(children.IsSuccess, Is.True);
            Assert.That(
                string.Join(",", children.Value!.Select(node => node.Name)),
                Is.EqualTo("References,Program.cs"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task ReferenceExpansionUsesEvaluatedProviderAndConciseProjectReferenceNames()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var project = Path.Combine(root, "App.csproj");
            File.WriteAllText(project, "<Project />");
            var references = new ProjectReferenceInfo[]
            {
                new("../Lib/ParcelBox.Application.csproj", ProjectReferenceKind.Project),
                new("NUnit", ProjectReferenceKind.Package),
                new("Microsoft.AspNetCore.App", ProjectReferenceKind.Framework),
            };
            var referenceProvider = new FakeProjectReferenceProvider(references);
            var service = new WorkspaceTreeService(new FakeSolutionProjectProvider([]), referenceProvider);

            var result = await service.GetChildrenAsync(new WorkspaceNode(project, "References", WorkspaceNodeKind.References));

            Assert.That(result.IsSuccess, Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(
                    string.Join(",", result.Value!.Select(node => node.Name)),
                    Is.EqualTo("ParcelBox.Application,NUnit,Microsoft.AspNetCore.App"));
                Assert.That(referenceProvider.LastProjectPath, Is.EqualTo(project));
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task CancelledExpansionPropagatesCancellation()
    {
        var service = CreateService();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await service.GetChildrenAsync(
                new WorkspaceNode(Path.GetTempPath(), "Temp", WorkspaceNodeKind.Folder),
                cancellation.Token));
    }

    private static WorkspaceTreeService CreateService() =>
        new(new FakeSolutionProjectProvider([]), new FakeProjectReferenceProvider([]));

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"toren-workspace-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FakeSolutionProjectProvider(IReadOnlyList<string> projects) : ISolutionProjectProvider
    {
        public string? LastSolutionPath { get; private set; }

        public Task<Result<IReadOnlyList<string>>> GetProjectPathsAsync(
            string solutionPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastSolutionPath = solutionPath;
            return Task.FromResult(Result.Success(projects));
        }
    }

    private sealed class FakeProjectReferenceProvider(IReadOnlyList<ProjectReferenceInfo> references) : IProjectReferenceProvider
    {
        public string? LastProjectPath { get; private set; }

        public Task<Result<IReadOnlyList<ProjectReferenceInfo>>> GetReferencesAsync(
            string projectPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastProjectPath = projectPath;
            return Task.FromResult(Result.Success(references));
        }
    }
}
