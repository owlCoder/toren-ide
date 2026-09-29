using NUnit.Framework;
using Toren.App.Testing.Services;
using Toren.Core.Results;
using Toren.DotNet.Testing.Contracts;
using Toren.DotNet.Testing.Models;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Models;

namespace Toren.UnitTests.Testing;

[TestFixture]
public sealed class WorkspaceTestDiscoveryServiceTests
{
    [Test]
    public async Task DiscoversOnlyTestProjectsAndKeepsProjectFailureLocal()
    {
        var testProject = CreateProject("Tests.One", true, ["net10.0"]);
        var failingProject = CreateProject("Tests.Two", true, ["net10.0", "net9.0"]);
        var appProject = CreateProject("App", false, ["net10.0"]);
        var graphService = new StubGraphService([testProject, failingProject, appProject]);
        var discoveryService = new StubTestDiscoveryService(failingProject.Path);
        var service = new WorkspaceTestDiscoveryService(graphService, discoveryService);
        var workspace = new WorkspaceDescriptor("/repo", "repo", WorkspaceKind.Folder);

        var result = await service.DiscoverAsync(workspace);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(discoveryService.Requests, Has.Count.EqualTo(2));
            Assert.That(discoveryService.Requests.Any(request => request.ProjectPath == appProject.Path), Is.False);
            Assert.That(discoveryService.Requests[0].TargetFramework, Is.EqualTo("net10.0"));
            Assert.That(discoveryService.Requests[1].TargetFramework, Is.Null);
            Assert.That(result.Value[0].Succeeded, Is.True);
            Assert.That(result.Value[0].Tests, Has.Count.EqualTo(1));
            Assert.That(result.Value[1].Succeeded, Is.False);
            Assert.That(result.Value[1].ErrorMessage, Is.EqualTo("Discovery failed."));
        });
    }

    private static WorkspaceProject CreateProject(
        string name,
        bool isTestProject,
        IReadOnlyList<string> targetFrameworks)
    {
        var path = Path.GetFullPath(Path.Combine("repo", $"{name}.csproj"));
        var metadata = new ProjectMetadata(
            targetFrameworks,
            "Library",
            name,
            name,
            isTestProject,
            false,
            null,
            null,
            null);
        return new WorkspaceProject(path, name, metadata, []);
    }

    private sealed class StubGraphService(IReadOnlyList<WorkspaceProject> projects)
        : IWorkspaceProjectGraphService
    {
        public Task<Result<WorkspaceProjectGraph>> LoadAsync(
            WorkspaceDescriptor workspace,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(new WorkspaceProjectGraph(projects)));
    }

    private sealed class StubTestDiscoveryService(string failingProjectPath)
        : IDotNetTestDiscoveryService
    {
        public List<DotNetTestDiscoveryRequest> Requests { get; } = [];

        public Task<Result<IReadOnlyList<DotNetTestCase>>> DiscoverAsync(
            DotNetTestDiscoveryRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (request.ProjectPath == failingProjectPath)
            {
                return Task.FromResult(
                    Result.Failure<IReadOnlyList<DotNetTestCase>>(
                        OperationError.Create("test.discovery.failed", "Discovery failed.")));
            }

            IReadOnlyList<DotNetTestCase> tests =
            [
                new("Tests.One.SampleTests.Passes", "Passes"),
            ];
            return Task.FromResult(Result.Success(tests));
        }
    }
}
