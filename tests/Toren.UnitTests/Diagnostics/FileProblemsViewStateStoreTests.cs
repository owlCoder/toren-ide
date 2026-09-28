using NUnit.Framework;
using Toren.App.Diagnostics.Adapters;
using Toren.App.Diagnostics.Models;

namespace Toren.UnitTests.Diagnostics;

[TestFixture]
public sealed class FileProblemsViewStateStoreTests
{
    [Test]
    public async Task MissingStateReturnsDefaultFilters()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var store = new FileProblemsViewStateStore(Path.Combine(root, "problems-view-state.json"));

            var loaded = await store.LoadAsync();

            Assert.Multiple(() =>
            {
                Assert.That(loaded.IsSuccess, Is.True);
                Assert.That(loaded.Value, Is.EqualTo(ProblemsViewState.Default));
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task SaveAndLoadRoundTripsSeverityFilters()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(root, "problems-view-state.json");
            var store = new FileProblemsViewStateStore(path);
            var state = new ProblemsViewState(
                ShowErrors: true,
                ShowWarnings: false,
                ShowInfo: false);

            var saved = await store.SaveAsync(state);
            var loaded = await store.LoadAsync();

            Assert.Multiple(() =>
            {
                Assert.That(saved.IsSuccess, Is.True);
                Assert.That(loaded.IsSuccess, Is.True);
                Assert.That(loaded.Value, Is.EqualTo(state));
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task InvalidStoredStateReturnsExplicitFailure()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(root, "problems-view-state.json");
            await File.WriteAllTextAsync(path, "{not-json");
            var store = new FileProblemsViewStateStore(path);

            var loaded = await store.LoadAsync();

            Assert.Multiple(() =>
            {
                Assert.That(loaded.IsFailure, Is.True);
                Assert.That(loaded.Error.Code, Is.EqualTo("problems.view-state.invalid"));
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"toren-problems-state-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
