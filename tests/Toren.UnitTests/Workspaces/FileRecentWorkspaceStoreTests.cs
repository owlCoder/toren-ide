using NUnit.Framework;
using Toren.Workspaces.Adapters;
using Toren.Workspaces.Models;

namespace Toren.UnitTests.Workspaces;

[TestFixture]
public sealed class FileRecentWorkspaceStoreTests
{
    [Test]
    public async Task MissingHistoryStartsEmptyAndRecordsStandardPaths()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var store = new FileRecentWorkspaceStore(Path.Combine(root, "state", "recent-workspaces.json"));
            var first = new WorkspaceDescriptor(Path.Combine(root, "First"), "First", WorkspaceKind.Folder);
            var second = new WorkspaceDescriptor(Path.Combine(root, "Second.slnx"), "Second", WorkspaceKind.SolutionX);

            var empty = await store.LoadAsync();
            var recordedFirst = await store.RecordAsync(first);
            var recordedSecond = await store.RecordAsync(second);
            var recordedAgain = await store.RecordAsync(first);
            var reloaded = await store.LoadAsync();

            Assert.Multiple(() =>
            {
                Assert.That(empty.IsSuccess, Is.True);
                Assert.That(empty.Value, Is.Empty);
                Assert.That(recordedFirst.IsSuccess, Is.True);
                Assert.That(recordedSecond.IsSuccess, Is.True);
                Assert.That(recordedAgain.IsSuccess, Is.True);
                Assert.That(reloaded.IsSuccess, Is.True);
                Assert.That(string.Join(",", reloaded.Value!.Select(workspace => workspace.DisplayName)), Is.EqualTo("First,Second"));
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task InvalidHistoryReturnsExpectedFailure()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var historyPath = Path.Combine(root, "recent-workspaces.json");
            File.WriteAllText(historyPath, "not json");
            var store = new FileRecentWorkspaceStore(historyPath);

            var result = await store.LoadAsync();

            Assert.Multiple(() =>
            {
                Assert.That(result.IsFailure, Is.True);
                Assert.That(result.Error.Code, Is.EqualTo("workspace.history.invalid"));
            });

            var workspace = new WorkspaceDescriptor(Path.Combine(root, "Recovered"), "Recovered", WorkspaceKind.Folder);
            var recovered = await store.RecordAsync(workspace);
            Assert.That(recovered.IsSuccess, Is.True);
            Assert.That((await store.LoadAsync()).Value?.Single().DisplayName, Is.EqualTo("Recovered"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"toren-history-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
