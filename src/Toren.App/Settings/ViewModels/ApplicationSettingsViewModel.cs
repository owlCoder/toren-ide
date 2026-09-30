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
    private int _saveKeybindingIndex;

    [ObservableProperty]
    private int _openSettingsKeybindingIndex;

    [ObservableProperty]
    private string _statusText = "Settings are stored locally for this Toren IDE installation.";

    public bool ShowAppearanceSection => MatchesSearch("appearance theme dark light color");

    public bool ShowEditorSection => MatchesSearch("editor font size line numbers word wrap wrapping");

    public bool ShowKeyboardSection => MatchesSearch("keyboard keybinding keybindings shortcut shortcuts save settings");

    public bool HasSearchResults => ShowAppearanceSection || ShowEditorSection || ShowKeyboardSection;

    public void Load(ApplicationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        ThemeIndex = settings.Theme == ApplicationThemePreference.Light ? 1 : 0;
        EditorFontSize = settings.EffectiveEditorFontSize;
        ShowLineNumbers = settings.EffectiveShowLineNumbers;
        WordWrap = settings.EffectiveWordWrap;
        SaveKeybindingIndex = settings.EffectiveSaveKeybinding switch
        {
            "primary+shift+s" => 1,
            "alt+s" => 2,
            _ => 0,
        };
        OpenSettingsKeybindingIndex = settings.EffectiveOpenSettingsKeybinding switch
        {
            "primary+alt+shift+s" => 1,
            "f12" => 2,
            _ => 0,
        };
    }

    public ApplicationSettings ToSettings() => new(
        ThemeIndex == 1 ? ApplicationThemePreference.Light : ApplicationThemePreference.Dark,
        Math.Clamp(EditorFontSize, 8d, 40d),
        ShowLineNumbers,
        WordWrap,
        SaveKeybindingIndex switch
        {
            1 => "primary+shift+s",
            2 => "alt+s",
            _ => ApplicationSettings.DefaultSaveKeybinding,
        },
        OpenSettingsKeybindingIndex switch
        {
            1 => "primary+alt+shift+s",
            2 => "f12",
            _ => ApplicationSettings.DefaultOpenSettingsKeybinding,
        });

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(ShowAppearanceSection));
        OnPropertyChanged(nameof(ShowEditorSection));
        OnPropertyChanged(nameof(ShowKeyboardSection));
        OnPropertyChanged(nameof(HasSearchResults));
    }

    private bool MatchesSearch(string keywords)
    {
        var query = SearchText.Trim();
        return query.Length == 0
            || keywords.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}
