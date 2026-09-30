using CommunityToolkit.Mvvm.ComponentModel;
using Toren.App.Settings.Models;

namespace Toren.App.Settings.ViewModels;

public sealed partial class ApplicationSettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _themeIndex;

    [ObservableProperty]
    private double _editorFontSize = ApplicationSettings.DefaultEditorFontSize;

    [ObservableProperty]
    private bool _showLineNumbers = ApplicationSettings.DefaultShowLineNumbers;

    [ObservableProperty]
    private bool _wordWrap = ApplicationSettings.DefaultWordWrap;

    [ObservableProperty]
    private string _statusText = "Settings are stored locally for this Toren IDE installation.";

    public bool ShowAppearanceSection => MatchesSearch("appearance theme dark light color");

    public bool ShowEditorSection => MatchesSearch("editor font size line numbers word wrap wrapping");

    public bool HasSearchResults => ShowAppearanceSection || ShowEditorSection;

    public void Load(ApplicationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        ThemeIndex = settings.Theme == ApplicationThemePreference.Light ? 1 : 0;
        EditorFontSize = settings.EffectiveEditorFontSize;
        ShowLineNumbers = settings.EffectiveShowLineNumbers;
        WordWrap = settings.EffectiveWordWrap;
    }

    public ApplicationSettings ToSettings() => new(
        ThemeIndex == 1 ? ApplicationThemePreference.Light : ApplicationThemePreference.Dark,
        Math.Clamp(EditorFontSize, 8d, 40d),
        ShowLineNumbers,
        WordWrap);

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(ShowAppearanceSection));
        OnPropertyChanged(nameof(ShowEditorSection));
        OnPropertyChanged(nameof(HasSearchResults));
    }

    private bool MatchesSearch(string keywords)
    {
        var query = SearchText.Trim();
        return query.Length == 0
            || keywords.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}
