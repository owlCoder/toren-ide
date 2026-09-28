using NUnit.Framework;
using Toren.App.Documents.Contracts;
using Toren.App.Documents.Models;
using Toren.App.Search.Models;
using Toren.App.Search.Services;
using Toren.Core.Results;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Models;

namespace Toren.UnitTests.Search;

[TestFixture]
public sealed class WorkspaceTextSearchServiceTests
{
    [Test]
    public async Task SearchFindsAllMatchesWithOneBasedLocations()
    {
        var root = Path.GetFullPath("/workspace");
        var file = Path.Combine(root, "src", "Sample.cs");
        var service = CreateService(
            [new WorkspaceFileEntry(file, "src/Sample.cs", "Sample.cs")],
            new Dictionary<string, string>
            {
                [file] = "alpha beta\nBeta beta",
            });

        var result = await service.SearchAsync(root, "beta", new WorkspaceTextSearchOptions());

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value, Has.Count.EqualTo(3));
            Assert.That(result.Value![0].Line, Is.EqualTo(1));
            Assert.That(result.Value[0].Column, Is.EqualTo(7));
            Assert.That(result.Value[1].Line, Is.EqualTo(2));
            Assert.That(result.Value[1].Column, Is.EqualTo(1));
            Assert.That(result.Value[2].Column, Is.EqualTo(6));
        });
    }

    [Test]
    public async Task SearchHonorsMatchCase()
    {
        var root = Path.GetFullPath("/workspace");
        var file = Path.Combine(root, "Sample.cs");
        var service = CreateService(
            [new WorkspaceFileEntry(file, "Sample.cs", "Sample.cs")],
            new Dictionary<string, string> { [file] = "Value value VALUE" });

        var result = await service.SearchAsync(
            root,
            "Value",
            new WorkspaceTextSearchOptions(MatchCase: true));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Has.Count.EqualTo(1));
        Assert.That(result.Value![0].Column, Is.EqualTo(1));
    }

    [Test]
    public async Task SearchUsesOpenDocumentOverrideInsteadOfStoredText()
    {
        var root = Path.GetFullPath("/workspace");
        var file = Path.Combine(root, "Sample.cs");
        var store = new FakeTextDocumentStore(new Dictionary<string, string>
        {
            [file] = "saved content",
        });
        var service = new WorkspaceTextSearchService(
            new FakeWorkspaceFileProvider([new WorkspaceFileEntry(file, "Sample.cs", "Sample.cs")]),
            store);
        IReadOnlyDictionary<string, string> overrides = new Dictionary<string, string>
        {
            [file] = "unsaved needle",
        };

        var result = await service.SearchAsync(
            root,
            "needle",
            new WorkspaceTextSearchOptions(),
            overrides);

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value, Has.Count.EqualTo(1));
            Assert.That(store.LoadCount, Is.Zero);
        });
    }

    [Test]
    public async Task SearchSkipsUnsupportedFilesAndRespectsMaximumResultCount()
    {
        var root = Path.GetFullPath("/workspace");
        var source = Path.Combine(root, "Sample.cs");
        var image = Path.Combine(root, "logo.png");
        var store = new FakeTextDocumentStore(new Dictionary<string, string>
        {
            [source] = "hit hit hit",
            [image] = "hit",
        });
        var service = new WorkspaceTextSearchService(
            new FakeWorkspaceFileProvider(
            [
                new WorkspaceFileEntry(source, "Sample.cs", "Sample.cs"),
                new WorkspaceFileEntry(image, "logo.png", "logo.png"),
            ]),
            store);

        var result = await service.SearchAsync(
            root,
            "hit",
            new WorkspaceTextSearchOptions(),
            maxResults: 2);

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value, Has.Count.EqualTo(2));
            Assert.That(store.LoadCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task SearchHonorsIncludeAndExcludePathPatternsBeforeLoadingFiles()
    {
        var root = Path.GetFullPath("/workspace");
        var appFile = Path.Combine(root, "src", "App", "Program.cs");
        var testFile = Path.Combine(root, "tests", "ProgramTests.cs");
        var jsonFile = Path.Combine(root, "src", "App", "appsettings.json");
        var store = new FakeTextDocumentStore(new Dictionary<string, string>
        {
            [appFile] = "needle",
            [testFile] = "needle",
            [jsonFile] = "needle",
        });
        var service = new WorkspaceTextSearchService(
            new FakeWorkspaceFileProvider(
            [
                new WorkspaceFileEntry(appFile, "src/App/Program.cs", "Program.cs"),
                new WorkspaceFileEntry(testFile, "tests/ProgramTests.cs", "ProgramTests.cs"),
                new WorkspaceFileEntry(jsonFile, "src/App/appsettings.json", "appsettings.json"),
            ]),
            store);

        var result = await service.SearchAsync(
            root,
            "needle",
            new WorkspaceTextSearchOptions(
                IncludePatterns: "*.cs",
                ExcludePatterns: "*Tests*"));

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value, Has.Count.EqualTo(1));
            Assert.That(result.Value![0].RelativePath, Is.EqualTo("src/App/Program.cs"));
            Assert.That(store.LoadCount, Is.EqualTo(1));
        });
    }

    private static WorkspaceTextSearchService CreateService(
        IReadOnlyList<WorkspaceFileEntry> files,
        IReadOnlyDictionary<string, string> textByPath) =>
        new(new FakeWorkspaceFileProvider(files), new FakeTextDocumentStore(textByPath));

    private sealed class FakeWorkspaceFileProvider(IReadOnlyList<WorkspaceFileEntry> files)
        : IWorkspaceFileProvider
    {
        public Task<Result<IReadOnlyList<WorkspaceFileEntry>>> GetFilesAsync(
            string workspacePath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Result.Success(files));
        }
    }

    private sealed class FakeTextDocumentStore(IReadOnlyDictionary<string, string> textByPath)
        : ITextDocumentStore
    {
        public int LoadCount { get; private set; }

        public Task<Result<TextDocumentContent>> LoadAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LoadCount++;
            var fullPath = Path.GetFullPath(path);
            if (!textByPath.TryGetValue(fullPath, out var text))
            {
                throw new InvalidOperationException($"No fake text configured for {fullPath}.");
            }

            return Task.FromResult(Result.Success(
                new TextDocumentContent(fullPath, text, TextDocumentEncoding.Utf8)));
        }

        public Task<Result<TextDocumentContent>> SaveAsync(
            TextDocumentContent document,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
