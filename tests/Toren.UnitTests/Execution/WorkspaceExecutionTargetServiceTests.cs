using NUnit.Framework;
using Toren.App.Execution.Services;
using Toren.Core.Results;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Models;

namespace Toren.UnitTests.Execution;

[TestFixture]
public sealed class WorkspaceExecutionTargetServiceTests
{
    [Test]
    public async Task ReturnsRunnableNonTestProjectsWithEvaluatedFrameworks()
    {
        var root = Path.Combine(Path.GetTempPath(), $"toren-targets-{Guid.NewGuid():N}");
        var appPath = Path.Combine(root, "App", "App.csproj");
        var desktopPath = Path.Combine(root, "Desktop", "Desktop.csproj");
        var graph = new WorkspaceProjectGraph(
            [
                CreateProject(appPath, "App", "Exe", isTestProject: false, ["net8.0", "net10.0"]),
                CreateProject(Path.Combine(root, "Library", "Library.csproj"), "Library", "Library", false, ["net10.0"]),
                CreateProject(Path.Combine(root, "Tests", "Tests.csproj"), "Tests", "Exe", true, ["net10.0"]),
                CreateProject(desktopPath, "Desktop", "WinExe", false, ["net10.0"]),
            ]);
        var service = new WorkspaceExecutionTargetService(new FakeProjectCatalog(Result.Success(graph.Projects)));

        var result = await service.GetTargetsAsync(
            new WorkspaceDescriptor(root, "Workspace", WorkspaceKind.Folder));

        Assert.That(result.IsSuccess, Is.True);
        var targets = result.Value!;
        Assert.Multiple(() =>
        {
            Assert.That(targets, Has.Count.EqualTo(2));
            Assert.That(targets[0].DisplayName, Is.EqualTo("App"));
            Assert.That(targets[0].ProjectPath, Is.EqualTo(Path.GetFullPath(appPath)));
            Assert.That(string.Join("|", targets[0].TargetFrameworks), Is.EqualTo("net8.0|net10.0"));
            Assert.That(targets[1].DisplayName, Is.EqualTo("Desktop"));
        });
    }

    [Test]
    public async Task ProjectGraphFailureIsPropagated()
    {
        var error = OperationError.Create("workspace.graph.failed", "Graph failed.");
        var service = new WorkspaceExecutionTargetService(
            new FakeProjectCatalog(Result.Failure<IReadOnlyList<WorkspaceProject>>(error)));

        var result = await service.GetTargetsAsync(
            new WorkspaceDescriptor(Path.GetTempPath(), "Workspace", WorkspaceKind.Folder));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("workspace.graph.failed"));
        });
    }

    private static WorkspaceProject CreateProject(
        string path,
        string displayName,
        string outputType,
        bool isTestProject,
        IReadOnlyList<string> targetFrameworks) =>
        new(
            path,
            displayName,
            new ProjectMetadata(
                targetFrameworks,
                outputType,
                displayName,
                displayName,
                isTestProject,
                false,
                null,
                null,
                null),
            []);

    private sealed class FakeProjectCatalog(Result<IReadOnlyList<WorkspaceProject>> result)
        : IWorkspaceProjectCatalog
    {
        public Task<Result<IReadOnlyList<WorkspaceProject>>> GetProjectsAsync(
            WorkspaceDescriptor workspace,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(result);
        }
    }
}
