using NUnit.Framework;
using Toren.App.Documents.Models;
using Toren.App.Layout.Adapters;
using Toren.App.Layout.Models;

namespace Toren.UnitTests.Editor;

[TestFixture]
public sealed class FileWorkspaceLayoutStoreTests
{
    private string _directory = string.Empty;
    private FileWorkspaceLayoutStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "toren-tests", $"layouts-{Guid.NewGuid():N}");
        _store = new FileWorkspaceLayoutStore(_directory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static string Workspace(string name) => Path.Combine(Path.GetTempPath(), "toren-tests", name);

    [Test]
    public async Task Load_WhenNothingWasSaved_ReturnsDefault()
    {
        var loaded = await _store.LoadAsync(Workspace("never-saved"));

        Assert.That(loaded.IsSuccess, Is.True);
        Assert.That(loaded.Value!.SplitOrientation, Is.EqualTo(EditorSplitOrientation.None));
        Assert.That(loaded.Value.SyncWithActiveDocument, Is.True);
    }

    [Test]
    public async Task SaveThenLoad_RoundTripsEveryField()
    {
        var workspace = Workspace("round-trip");
        var layout = new WorkspaceLayoutState(
            SidebarWidth: 300,
            ToolPanelHeight: 350,
            SplitOrientation: EditorSplitOrientation.Vertical,
            SplitRatio: 0.4,
            SecondaryDocumentPath: Path.Combine(workspace, "b.cs"),
            PinnedDocumentPaths: [Path.Combine(workspace, "a.cs")],
            SyncWithActiveDocument: false);

        var saved = await _store.SaveAsync(workspace, layout);
        var loaded = await _store.LoadAsync(workspace);

        Assert.That(saved.IsSuccess, Is.True);
        Assert.That(loaded.IsSuccess, Is.True);
        var value = loaded.Value!;
        Assert.That(value.SidebarWidth, Is.EqualTo(300));
        Assert.That(value.ToolPanelHeight, Is.EqualTo(350));
        Assert.That(value.SplitOrientation, Is.EqualTo(EditorSplitOrientation.Vertical));
        Assert.That(value.SplitRatio, Is.EqualTo(0.4));
        Assert.That(value.SecondaryDocumentPath, Is.EqualTo(layout.SecondaryDocumentPath));
        Assert.That(value.PinnedDocumentPaths, Is.EqualTo(layout.PinnedDocumentPaths));
        Assert.That(value.SyncWithActiveDocument, Is.False);
    }

    [Test]
    public async Task EachWorkspaceHasItsOwnLayout()
    {
        await _store.SaveAsync(Workspace("one"), new WorkspaceLayoutState(SidebarWidth: 400));

        var other = await _store.LoadAsync(Workspace("two"));

        Assert.That(other.Value!.SidebarWidth, Is.Null);
    }

    [Test]
    public async Task Save_WithInvalidRatio_Fails()
    {
        var saved = await _store.SaveAsync(Workspace("bad"), new WorkspaceLayoutState(SplitRatio: 2));

        Assert.That(saved.IsSuccess, Is.False);
    }

    [Test]
    public async Task Load_WithCorruptedFile_FailsInsteadOfThrowing()
    {
        var workspace = Workspace("corrupt");
        await _store.SaveAsync(workspace, new WorkspaceLayoutState(SidebarWidth: 300));
        foreach (var file in Directory.GetFiles(_directory, "*.json"))
        {
            await File.WriteAllTextAsync(file, "this is not json");
        }

        var loaded = await _store.LoadAsync(workspace);

        Assert.That(loaded.IsSuccess, Is.False);
    }

    [Test]
    public async Task Save_LeavesNoTemporaryFilesBehind()
    {
        await _store.SaveAsync(Workspace("clean"), new WorkspaceLayoutState(SidebarWidth: 300));

        Assert.That(Directory.GetFiles(_directory, "*.tmp"), Is.Empty);
    }
}
