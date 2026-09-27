using NUnit.Framework;
using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.Workspaces.Models;
using Toren.Workspaces.Services;

namespace Toren.UnitTests.Workspaces;

[TestFixture]
public sealed class WorkspaceTreeServiceTests
{
    [Test]
    public void CreateRootRejectsMissingPath()
    {
        var service = new WorkspaceTreeService(new FakeProcessRunner(string.Empty));
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

            var service = new WorkspaceTreeService(new FakeProcessRunner(string.Empty));
            var result = await service.GetChildrenAsync(new WorkspaceNode(root, "Sample", WorkspaceNodeKind.Folder));

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
    public async Task SolutionChildrenUseDotnetProjectList()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var solution = Path.Combine(root, "Sample.slnx");
            File.WriteAllText(solution, "<Solution />");
            var runner = new FakeProcessRunner("Project(s)\n----------\nsrc/App One/App.csproj\nsrc\\Library\\Library.csproj\n");
            var service = new WorkspaceTreeService(runner);

            var result = await service.GetChildrenAsync(new WorkspaceNode(solution, "Sample", WorkspaceNodeKind.Solution));

            Assert.That(result.IsSuccess, Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(string.Join(",", result.Value!.Select(node => node.Name)), Is.EqualTo("App,Library"));
                Assert.That(result.Value![0].Path, Is.EqualTo(Path.Combine(root, "src", "App One", "App.csproj")));
                Assert.That(result.Value.All(node => node.Kind == WorkspaceNodeKind.Project), Is.True);
                Assert.That(string.Join("|", runner.LastRequest!.Arguments), Is.EqualTo($"sln|{solution}|list"));
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task ProjectChildrenExposeEvaluatedReferenceNamesAndPhysicalFiles()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var project = Path.Combine(root, "App.csproj");
            File.WriteAllText(
                project,
                "<Project><ItemGroup><ProjectReference Include=\"../Lib/ParcelBox.Application.csproj\" /><PackageReference Include=\"NUnit\" Version=\"4.0\" /><FrameworkReference Include=\"Microsoft.AspNetCore.App\" /></ItemGroup></Project>");
            File.WriteAllText(Path.Combine(root, "Program.cs"), "class Program {}");
            var runner = new FakeProcessRunner(
                "{\"Items\":{\"ProjectReference\":[{\"Identity\":\"../Lib/ParcelBox.Application.csproj\"}],\"PackageReference\":[{\"Identity\":\"NUnit\"}],\"FrameworkReference\":[{\"Identity\":\"Microsoft.AspNetCore.App\"}]}}");
            var service = new WorkspaceTreeService(runner);

            var children = await service.GetChildrenAsync(new WorkspaceNode(project, "App", WorkspaceNodeKind.Project));
            var references = await service.GetChildrenAsync(new WorkspaceNode(project, "References", WorkspaceNodeKind.References));

            Assert.Multiple(() =>
            {
                Assert.That(children.IsSuccess, Is.True);
                Assert.That(string.Join(",", children.Value!.Select(node => node.Name)), Is.EqualTo("References,Program.cs"));
                Assert.That(references.IsSuccess, Is.True);
                Assert.That(
                    string.Join(",", references.Value!.Select(node => node.Name)),
                    Is.EqualTo("ParcelBox.Application,NUnit,Microsoft.AspNetCore.App"));
                Assert.That(
                    string.Join("|", runner.LastRequest!.Arguments),
                    Is.EqualTo($"msbuild|{project}|-nologo|-verbosity:quiet|-getItem:ProjectReference,PackageReference,FrameworkReference"));
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task ReferenceExpansionUsesEvaluatedItemsInsteadOfInactiveDeclarations()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var project = Path.Combine(root, "App.csproj");
            File.WriteAllText(
                project,
                "<Project><ItemGroup><ProjectReference Include=\"Active.csproj\" /><ProjectReference Include=\"Inactive.csproj\" Condition=\"'$(Configuration)' == 'Never'\" /></ItemGroup></Project>");
            var runner = new FakeProcessRunner(
                "{\"Items\":{\"ProjectReference\":[{\"Identity\":\"Active.csproj\"}],\"PackageReference\":[],\"FrameworkReference\":[]}}");
            var service = new WorkspaceTreeService(runner);

            var references = await service.GetChildrenAsync(new WorkspaceNode(project, "References", WorkspaceNodeKind.References));

            Assert.Multiple(() =>
            {
                Assert.That(references.IsSuccess, Is.True);
                Assert.That(references.Value!.Select(node => node.Name), Is.EqualTo(new[] { "Active" }));
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task ReferenceExpansionReportsMsBuildEvaluationFailure()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var project = Path.Combine(root, "App.csproj");
            File.WriteAllText(project, "<Project />");
            var service = new WorkspaceTreeService(
                new FakeProcessRunner(string.Empty, exitCode: 1, standardError: "evaluation failed"));

            var references = await service.GetChildrenAsync(new WorkspaceNode(project, "References", WorkspaceNodeKind.References));

            Assert.Multiple(() =>
            {
                Assert.That(references.IsFailure, Is.True);
                Assert.That(references.Error.Code, Is.EqualTo("workspace.project.evaluate.failed"));
                Assert.That(references.Error.Message, Does.Contain("evaluation failed"));
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void CancelledExpansionPropagatesCancellation()
    {
        var service = new WorkspaceTreeService(new FakeProcessRunner(string.Empty));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await service.GetChildrenAsync(
                new WorkspaceNode(Path.GetTempPath(), "Temp", WorkspaceNodeKind.Folder),
                cancellation.Token));
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"toren-workspace-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FakeProcessRunner(
        string output,
        int exitCode = 0,
        string standardError = "") : IProcessRunner
    {
        public ProcessRequest? LastRequest { get; private set; }

        public Task<Result<ProcessResult>> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(Result.Success(new ProcessResult(exitCode, output, standardError)));
        }
    }
}
