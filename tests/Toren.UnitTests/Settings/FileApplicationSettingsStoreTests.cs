using NUnit.Framework;
using Toren.App.Settings.Adapters;
using Toren.App.Settings.Models;

namespace Toren.UnitTests.Settings;

[TestFixture]
public sealed class FileApplicationSettingsStoreTests
{
    [Test]
    public async Task MissingFileReturnsDefaults()
    {
        using var directory = new TemporaryDirectory();
        var store = new FileApplicationSettingsStore(Path.Combine(directory.Path, "settings.json"));

        var result = await store.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(ApplicationSettings.Default));
            Assert.That(result.Value!.Theme, Is.EqualTo(ApplicationThemePreference.Dark));
            Assert.That(result.Value.EffectiveEditorFontSize, Is.EqualTo(ApplicationSettings.DefaultEditorFontSize));
            Assert.That(result.Value.EffectiveShowLineNumbers, Is.True);
            Assert.That(result.Value.EffectiveWordWrap, Is.False);
        });
    }

    [Test]
    public async Task SaveAndLoadRoundTripsApplicationPreferences()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        var store = new FileApplicationSettingsStore(path);
        var expected = new ApplicationSettings(
            ApplicationThemePreference.Light,
            EditorFontSize: 16,
            ShowLineNumbers: false,
            WordWrap: true);

        var saved = await store.SaveAsync(expected);
        var loaded = await store.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(saved.IsSuccess, Is.True);
            Assert.That(loaded.IsSuccess, Is.True);
            Assert.That(loaded.Value, Is.EqualTo(expected));
            Assert.That(File.ReadAllText(path), Does.Contain("\"Version\":1"));
            Assert.That(File.ReadAllText(path), Does.Contain("\"Theme\":\"light\""));
            Assert.That(File.ReadAllText(path), Does.Contain("\"EditorFontSize\":16"));
        });
    }

    [Test]
    public async Task LegacyThemeOnlyFileLoadsEditorDefaults()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        await File.WriteAllTextAsync(path, "{\"Version\":1,\"Theme\":\"light\"}");
        var store = new FileApplicationSettingsStore(path);

        var result = await store.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value!.Theme, Is.EqualTo(ApplicationThemePreference.Light));
            Assert.That(result.Value.EffectiveEditorFontSize, Is.EqualTo(ApplicationSettings.DefaultEditorFontSize));
            Assert.That(result.Value.EffectiveShowLineNumbers, Is.True);
            Assert.That(result.Value.EffectiveWordWrap, Is.False);
        });
    }

    [Test]
    public async Task ThemeOnlySavePreservesEditorPreferences()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        var store = new FileApplicationSettingsStore(path);
        await store.SaveAsync(new ApplicationSettings(
            ApplicationThemePreference.Dark,
            EditorFontSize: 18,
            ShowLineNumbers: false,
            WordWrap: true));

        var saved = await store.SaveAsync(new ApplicationSettings(ApplicationThemePreference.Light));
        var loaded = await store.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(saved.IsSuccess, Is.True);
            Assert.That(loaded.IsSuccess, Is.True);
            Assert.That(loaded.Value!.Theme, Is.EqualTo(ApplicationThemePreference.Light));
            Assert.That(loaded.Value.EffectiveEditorFontSize, Is.EqualTo(18));
            Assert.That(loaded.Value.EffectiveShowLineNumbers, Is.False);
            Assert.That(loaded.Value.EffectiveWordWrap, Is.True);
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

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error.Code, Is.EqualTo("app.settings.invalid-format"));
    }

    [Test]
    public async Task InvalidStoredEditorFontSizeReturnsStableFormatError()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        await File.WriteAllTextAsync(
            path,
            "{\"Version\":1,\"Theme\":\"dark\",\"EditorFontSize\":72}");
        var store = new FileApplicationSettingsStore(path);

        var result = await store.LoadAsync();

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error.Code, Is.EqualTo("app.settings.invalid-format"));
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
