using NUnit.Framework;
using Toren.App.Settings.Adapters;
using Toren.App.Settings.Models;

namespace Toren.UnitTests.Settings;

[TestFixture]
public sealed class FileApplicationSettingsStoreTests
{
    [Test]
    public async Task MissingFileReturnsDarkDefault()
    {
        using var directory = new TemporaryDirectory();
        var store = new FileApplicationSettingsStore(Path.Combine(directory.Path, "settings.json"));

        var result = await store.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(ApplicationSettings.Default));
            Assert.That(result.Value!.Theme, Is.EqualTo(ApplicationThemePreference.Dark));
        });
    }

    [Test]
    public async Task SaveAndLoadRoundTripThemePreference()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        var store = new FileApplicationSettingsStore(path);

        var saved = await store.SaveAsync(new ApplicationSettings(ApplicationThemePreference.Light));
        var loaded = await store.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(saved.IsSuccess, Is.True);
            Assert.That(loaded.IsSuccess, Is.True);
            Assert.That(loaded.Value!.Theme, Is.EqualTo(ApplicationThemePreference.Light));
            Assert.That(File.ReadAllText(path), Does.Contain("\"Version\":1"));
            Assert.That(File.ReadAllText(path), Does.Contain("\"Theme\":\"light\""));
        });
    }

    [Test]
    public async Task InvalidStoredThemeReturnsStableFormatError()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        await File.WriteAllTextAsync(path, "{\"Version\":1,\"Theme\":\"system\"}");
        var store = new FileApplicationSettingsStore(path);

        var result = await store.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("app.settings.invalid-format"));
        });
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"toren-settings-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
