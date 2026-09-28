using NUnit.Framework;
using Toren.App.Diagnostics.Services;
using Toren.Core.Results;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Models;
using Toren.Workspaces.Services;

namespace Toren.UnitTests.Diagnostics;

[TestFixture]
public sealed class WorkspaceProblemsScopeServiceTests
{
    [Test]
    public async Task BuildAssignsFilesToNearestOwningProject()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var outerDirectory = Path.Combine(root, "Outer");
            var nestedDirectory = Path.Combine(outerDirectory, "Nested");
            Directory.CreateDirectory(nestedDirectory);
            var outerFile = Path.Combine(outerDirectory, "Outer.cs");
            var nestedFile = Path.Combine(nestedDirectory, "Inner.cs");
            var outer = CreateProject(Path.Combine(outerDirectory, "Outer.csproj"), "Outer");
            var nested = CreateProject(Path.Combine(nestedDirectory, "Nested.csproj"), "Nested");
            var graph = new WorkspaceProjectGraph([outer, nested]);
            var files = new WorkspaceFileEntry[]
            {
                new(outerFile, Path.Combine("Outer", "Outer.cs"), "Outer.cs"),
                new(nestedFile, Path.Combine("Outer", "Nested", "Inner.cs"), "Inner.cs"),
            };
            var service = new WorkspaceProblemsScopeService(
                new WorkspaceClassifier(),
                new StubProjectGraphService(graph),
                new StubWorkspaceFileProvider(files));

            var result = await service.BuildAsync(root);

            Assert.That(result.IsSuccess, Is.True);
            var index = result.Value ?? throw new AssertionException("Expected a Problems scope index.");
            var outerScope = index.Projects.Single(project => project.DisplayName == "Outer");
            var nestedScope = index.Projects.Single(project => project.DisplayName == "Nested");
            Assert.Multiple(() =>
            {
                Assert.That(outerScope.FilePaths, Is.EqualTo([Path.GetFullPath(outerFile)]));
                Assert.That(nestedScope.FilePaths, Is.EqualTo([Path.GetFullPath(nestedFile)]));
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static WorkspaceProject CreateProject(string path, string displayName) =>
        new(
            path,
            displayName,
            new ProjectMetadata([], null, null, null, false, false, null, null, null),
            []);

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"toren-problems-scope-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class StubProjectGraphService(WorkspaceProjectGraph graph) : IWorkspaceProjectGraphService
    {
        public Task<Result<WorkspaceProjectGraph>> LoadAsync(
            WorkspaceDescriptor workspace,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Result.Success(graph));
        }
    }

    private sealed class StubWorkspaceFileProvider(IReadOnlyList<WorkspaceFileEntry> files) : IWorkspaceFileProvider
    {
        public Task<Result<IReadOnlyList<WorkspaceFileEntry>>> GetFilesAsync(
            string workspacePath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Result.Success(files));
        }
    }
}
