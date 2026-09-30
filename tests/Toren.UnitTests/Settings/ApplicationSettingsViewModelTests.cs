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
            WordWrap: true);

        viewModel.Load(settings);
        var snapshot = viewModel.ToSettings();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ThemeIndex, Is.EqualTo(1));
            Assert.That(viewModel.EditorFontSize, Is.EqualTo(17));
            Assert.That(viewModel.ShowLineNumbers, Is.False);
            Assert.That(viewModel.WordWrap, Is.True);
            Assert.That(snapshot, Is.EqualTo(settings));
        });
    }

    [TestCase("theme", true, false)]
    [TestCase("word wrap", false, true)]
    [TestCase("missing", false, false)]
    public void SearchFiltersSettingsSections(
        string query,
        bool expectedAppearance,
        bool expectedEditor)
    {
        var viewModel = new ApplicationSettingsViewModel
        {
            SearchText = query,
        };

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ShowAppearanceSection, Is.EqualTo(expectedAppearance));
            Assert.That(viewModel.ShowEditorSection, Is.EqualTo(expectedEditor));
            Assert.That(viewModel.HasSearchResults, Is.EqualTo(expectedAppearance || expectedEditor));
        });
    }

    [Test]
    public void SnapshotClampsEditorFontSize()
    {
        var viewModel = new ApplicationSettingsViewModel
        {
            EditorFontSize = 100,
        };

        Assert.That(viewModel.ToSettings().EffectiveEditorFontSize, Is.EqualTo(40));
    }
}
