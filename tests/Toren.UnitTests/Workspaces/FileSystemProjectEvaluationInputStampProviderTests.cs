using NUnit.Framework;
using Toren.Core.Results;
using Toren.Workspaces.Adapters;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Models;
using Toren.Workspaces.Services;

namespace Toren.UnitTests.Workspaces;

[TestFixture]
public sealed class FileSystemProjectEvaluationInputStampProviderTests
{
    private string _parent = string.Empty;
    private string _root = string.Empty;
    private string _project = string.Empty;
    private WorkspaceDescriptor _workspace = null!;
    private FileSystemProjectEvaluationInputStampProvider _provider = null!;

    [SetUp]
    public void SetUp()
    {
        _parent = Path.Combine(Path.GetTempPath(), $"toren-stamp-{Guid.NewGuid():N}");
        _root = Path.Combine(_parent, "Workspace");
        _project = Path.Combine(_root, "App", "App.csproj");
        Directory.CreateDirectory(Path.Combine(_root, "App", "obj"));
        File.WriteAllText(_project, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.WriteAllText(Path.Combine(_root, "App", "Program.cs"), "class Program { }");
        File.WriteAllText(Path.Combine(_root, "App.sln"), "solution");
        _workspace = new WorkspaceDescriptor(Path.Combine(_root, "App.sln"), "App", WorkspaceKind.Solution);
        _provider = new FileSystemProjectEvaluationInputStampProvider(new FileSystemWorkspaceFileProvider());
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_parent, recursive: true);

    [Test]
    public async Task WorkspaceStampIsStableWhileNothingChanges()
    {
        var first = await _provider.GetWorkspaceStampAsync(_workspace);
        var second = await _provider.GetWorkspaceStampAsync(_workspace);
        var asFolder = await _provider.GetWorkspaceStampAsync(
            new WorkspaceDescriptor(_root, "Workspace", WorkspaceKind.Folder));

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.Not.Empty);
            Assert.That(second, Is.EqualTo(first));
            Assert.That(asFolder, Is.EqualTo(first));
        });
    }

    [Test]
    public async Task WorkspaceStampIgnoresSourceEditsAndBuildOutput()
    {
        var before = await _provider.GetWorkspaceStampAsync(_workspace);

        File.WriteAllText(Path.Combine(_root, "App", "Program.cs"), "class Program { static void Main() { } }");
        File.WriteAllText(Path.Combine(_root, "App", "obj", "App.AssemblyInfo.cs"), "// generated");
        Directory.CreateDirectory(Path.Combine(_root, "App", "bin"));
        File.WriteAllText(Path.Combine(_root, "App", "bin", "App.dll"), "binary");

        Assert.That(await _provider.GetWorkspaceStampAsync(_workspace), Is.EqualTo(before));
    }

    [TestCase("App/Added.cs")]
    [TestCase("README.md")]
    [TestCase("App/Views/Index.cshtml")]
    public async Task WorkspaceStampChangesWhenAFileIsAddedOrRemoved(string relativePath)
    {
        var before = await _provider.GetWorkspaceStampAsync(_workspace);
        var path = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Empty);
        var added = await _provider.GetWorkspaceStampAsync(_workspace);
        File.Delete(path);
        var removed = await _provider.GetWorkspaceStampAsync(_workspace);

        Assert.Multiple(() =>
        {
            Assert.That(added, Is.Not.EqualTo(before));
            Assert.That(removed, Is.EqualTo(before));
        });
    }

    [Test]
    public async Task WorkspaceStampChangesWhenAFileIsRenamed()
    {
        var before = await _provider.GetWorkspaceStampAsync(_workspace);

        File.Move(Path.Combine(_root, "App", "Program.cs"), Path.Combine(_root, "App", "Startup.cs"));

        Assert.That(await _provider.GetWorkspaceStampAsync(_workspace), Is.Not.EqualTo(before));
    }

    [TestCase("App/App.csproj")]
    [TestCase("App.sln")]
    [TestCase("Directory.Build.props")]
    [TestCase("build/Common.targets")]
    [TestCase("global.json")]
    [TestCase("NuGet.config")]
    [TestCase("App/packages.lock.json")]
    [TestCase("Directory.Build.rsp")]
    public async Task WorkspaceStampChangesWhenAnEvaluationInputIsModified(string relativePath)
    {
        var path = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "<Project />");
        var before = await _provider.GetWorkspaceStampAsync(_workspace);

        File.WriteAllText(path, "<Project><!-- edited --></Project>");
        var contentChanged = await _provider.GetWorkspaceStampAsync(_workspace);
        // Same length, later timestamp: detected through the modification time alone.
        File.WriteAllText(path, "<Project><!-- EDITED --></Project>");
        File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddSeconds(5));
        var touched = await _provider.GetWorkspaceStampAsync(_workspace);

        Assert.Multiple(() =>
        {
            Assert.That(contentChanged, Is.Not.EqualTo(before));
            Assert.That(touched, Is.Not.EqualTo(contentChanged));
        });
    }

    [TestCase("Directory.Build.props")]
    [TestCase("Directory.Packages.props")]
    [TestCase("global.json")]
    public async Task WorkspaceStampTracksInputsAboveTheWorkspaceDirectory(string fileName)
    {
        var before = await _provider.GetWorkspaceStampAsync(_workspace);
        var path = Path.Combine(_parent, fileName);

        File.WriteAllText(path, "<Project />");
        var created = await _provider.GetWorkspaceStampAsync(_workspace);
        File.WriteAllText(path, "<Project><!-- edited --></Project>");
        var edited = await _provider.GetWorkspaceStampAsync(_workspace);

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.Not.EqualTo(before));
            Assert.That(edited, Is.Not.EqualTo(created));
        });
    }

    [Test]
    public async Task UnavailableWorkspaceHasAStableStamp()
    {
        var missing = new WorkspaceDescriptor(Path.Combine(_parent, "Missing"), "Missing", WorkspaceKind.Folder);

        var first = await _provider.GetWorkspaceStampAsync(missing);
        var second = await _provider.GetWorkspaceStampAsync(missing);

        Assert.Multiple(() =>
        {
            Assert.That(first, Does.StartWith("unavailable:"));
            Assert.That(second, Is.EqualTo(first));
        });
    }

    [Test]
    public async Task ProjectStampTracksAssetsFileAndImportsFoundByEvaluation()
    {
        var assets = Path.Combine(_root, "App", "obj", "project.assets.json");
        var externalProps = Path.Combine(_parent, "Shared", "Directory.Build.props");
        Directory.CreateDirectory(Path.GetDirectoryName(externalProps)!);
        File.WriteAllText(externalProps, "<Project />");
        var projects = new[] { CreateProject(_project, assets, externalProps), CreateProject(_project, assets, externalProps) };

        var unrestored = await _provider.GetProjectStampAsync(projects);
        File.WriteAllText(assets, "{}");
        var restored = await _provider.GetProjectStampAsync(projects);
        var unchanged = await _provider.GetProjectStampAsync(projects);
        File.WriteAllText(externalProps, "<Project><!-- edited --></Project>");
        File.SetLastWriteTimeUtc(externalProps, new DateTime(2040, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var importEdited = await _provider.GetProjectStampAsync(projects);

        Assert.Multiple(() =>
        {
            Assert.That(restored.Value, Is.Not.EqualTo(unrestored.Value));
            Assert.That(unchanged, Is.EqualTo(restored));
            Assert.That(restored.LastWriteTimeUtc, Is.EqualTo(File.GetLastWriteTimeUtc(assets)));
            Assert.That(importEdited.Value, Is.Not.EqualTo(restored.Value));
            Assert.That(importEdited.LastWriteTimeUtc, Is.EqualTo(new DateTime(2040, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        });
    }

    [Test]
    public async Task ProjectStampOfNoProjectsIsStable()
    {
        var first = await _provider.GetProjectStampAsync([]);
        var second = await _provider.GetProjectStampAsync([]);

        Assert.Multiple(() =>
        {
            Assert.That(second, Is.EqualTo(first));
            Assert.That(first.LastWriteTimeUtc, Is.EqualTo(DateTime.MinValue));
        });
    }

    [Test]
    public void CancellationIsObserved()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Multiple(() =>
        {
            Assert.CatchAsync<OperationCanceledException>(
                async () => await _provider.GetWorkspaceStampAsync(_workspace, cancellation.Token));
            Assert.CatchAsync<OperationCanceledException>(
                async () => await _provider.GetProjectStampAsync([], cancellation.Token));
        });
    }

    [Test]
    public async Task SnapshotServiceReevaluatesOnlyWhenTrackedFilesChange()
    {
        var assets = Path.Combine(_root, "App", "obj", "project.assets.json");
        var evaluations = new CountingEvaluationProvider(assets);
        using var service = new WorkspaceProjectGraphService(
            new WorkspaceProjectGraphEvaluator(
                new FileSystemFolderProjectProvider(),
                new FixedSolutionProjectProvider([_project]),
                evaluations),
            _provider);

        await service.LoadAsync(_workspace);
        await service.LoadAsync(_workspace);
        File.WriteAllText(Path.Combine(_root, "App", "Program.cs"), "class Program { /* edited */ }");
        await service.LoadAsync(_workspace);
        var afterSourceEdit = evaluations.Count;

        File.WriteAllText(Path.Combine(_root, "App", "Added.cs"), "class Added { }");
        await service.LoadAsync(_workspace);
        var afterAddedFile = evaluations.Count;

        File.WriteAllText(_project, "<Project Sdk=\"Microsoft.NET.Sdk\"><!-- package added --></Project>");
        await service.LoadAsync(_workspace);
        var afterProjectEdit = evaluations.Count;

        File.WriteAllText(assets, "{ \"restored\": true }");
        File.SetLastWriteTimeUtc(assets, DateTime.UtcNow.AddMinutes(-1));
        await service.LoadAsync(_workspace);
        await service.LoadAsync(_workspace);
        var afterRestore = evaluations.Count;

        Assert.Multiple(() =>
        {
            Assert.That(afterSourceEdit, Is.EqualTo(1));
            Assert.That(afterAddedFile, Is.EqualTo(2));
            Assert.That(afterProjectEdit, Is.EqualTo(3));
            Assert.That(afterRestore, Is.EqualTo(4));
        });
    }

    private static WorkspaceProject CreateProject(string path, string assetsFile, string directoryBuildProps) =>
        new(
            path,
            "App",
            new ProjectMetadata(["net10.0"], "Exe", "App", "App", false, false, directoryBuildProps, null, null)
            {
                ProjectAssetsFilePath = assetsFile,
            },
            []);

    private sealed class CountingEvaluationProvider(string assetsFile) : IProjectEvaluationProvider
    {
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public Task<Result<IReadOnlyList<ProjectEvaluation>>> EvaluateAsync(
            IReadOnlyList<string> projectPaths,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            return Task.FromResult(Result.Success<IReadOnlyList<ProjectEvaluation>>(projectPaths
                .Select(_ => new ProjectEvaluation(
                    new ProjectMetadata(["net10.0"], "Exe", "App", "App", false, false, null, null, null)
                    {
                        ProjectAssetsFilePath = assetsFile,
                    },
                    []))
                .ToArray()));
        }

        public Task<Result<IReadOnlyList<ProjectMetadata>>> ResolveCompilerInputsAsync(
            IReadOnlyList<WorkspaceProject> projects,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success<IReadOnlyList<ProjectMetadata>>(projects
                .Select(static project => project.Metadata with { ReferencePaths = [] })
                .ToArray()));
    }

    private sealed class FixedSolutionProjectProvider(IReadOnlyList<string> projects) : ISolutionProjectProvider
    {
        public Task<Result<IReadOnlyList<string>>> GetProjectPathsAsync(
            string solutionPath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(projects));
    }
}
