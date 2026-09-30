using NUnit.Framework;
using Toren.App.Settings.Models;
using Toren.App.Settings.ViewModels;

namespace Toren.UnitTests.Settings;

[TestFixture]
public sealed class ApplicationSettingsViewModelTests
{
    [Test]
    public void LoadAndSnapshotRoundTripSettings()
    {
        var viewModel = new ApplicationSettingsViewModel();
        var settings = new ApplicationSettings(
            ApplicationThemePreference.Light,
            EditorFontSize: 17,
            ShowLineNumbers: false,
            WordWrap: true,
            SaveKeybinding: "alt+s",
            OpenSettingsKeybinding: "f12");

        viewModel.Load(settings);
        var snapshot = viewModel.ToSettings();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ThemeIndex, Is.EqualTo(1));
            Assert.That(viewModel.EditorFontSize, Is.EqualTo(17));
            Assert.That(viewModel.ShowLineNumbers, Is.False);
            Assert.That(viewModel.WordWrap, Is.True);
            Assert.That(viewModel.SaveKeybindingIndex, Is.EqualTo(2));
            Assert.That(viewModel.OpenSettingsKeybindingIndex, Is.EqualTo(2));
            Assert.That(snapshot, Is.EqualTo(settings));
        });
    }

    [TestCase("theme", true, false, false)]
    [TestCase("word wrap", false, true, false)]
    [TestCase("shortcut", false, false, true)]
    [TestCase("missing", false, false, false)]
    public void SearchFiltersSettingsSections(
        string query,
        bool expectedAppearance,
        bool expectedEditor,
        bool expectedKeyboard)
    {
        var viewModel = new ApplicationSettingsViewModel
        {
            SearchText = query,
        };

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ShowAppearanceSection, Is.EqualTo(expectedAppearance));
            Assert.That(viewModel.ShowEditorSection, Is.EqualTo(expectedEditor));
            Assert.That(viewModel.ShowKeyboardSection, Is.EqualTo(expectedKeyboard));
            Assert.That(
                viewModel.HasSearchResults,
                Is.EqualTo(expectedAppearance || expectedEditor || expectedKeyboard));
        });
    }

    [Test]
    public void SnapshotClampsEditorFontSizeAndMapsKeybindingPresets()
    {
        var viewModel = new ApplicationSettingsViewModel
        {
            EditorFontSize = 100,
            SaveKeybindingIndex = 1,
            OpenSettingsKeybindingIndex = 1,
        };

        var settings = viewModel.ToSettings();

        Assert.Multiple(() =>
        {
            Assert.That(settings.EffectiveEditorFontSize, Is.EqualTo(40));
            Assert.That(settings.EffectiveSaveKeybinding, Is.EqualTo("primary+shift+s"));
            Assert.That(settings.EffectiveOpenSettingsKeybinding, Is.EqualTo("primary+alt+shift+s"));
        });
    }
}
