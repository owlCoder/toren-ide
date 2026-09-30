namespace Toren.App.Settings.Models;

public enum ApplicationThemePreference
{
    Dark = 0,
    Light = 1,
}

public sealed record ApplicationSettings(
    ApplicationThemePreference Theme,
    double? EditorFontSize = null,
    bool? ShowLineNumbers = null,
    bool? WordWrap = null,
    string? SaveKeybinding = null,
    string? OpenSettingsKeybinding = null)
{
    public const double DefaultEditorFontSize = 13d;
    public const bool DefaultShowLineNumbers = true;
    public const bool DefaultWordWrap = false;
    public const string DefaultSaveKeybinding = "primary+s";
    public const string DefaultOpenSettingsKeybinding = "primary+alt+s";

    public static ApplicationSettings Default { get; } = new(
        ApplicationThemePreference.Dark,
        DefaultEditorFontSize,
        DefaultShowLineNumbers,
        DefaultWordWrap,
        DefaultSaveKeybinding,
        DefaultOpenSettingsKeybinding);

    public double EffectiveEditorFontSize => EditorFontSize ?? DefaultEditorFontSize;

    public bool EffectiveShowLineNumbers => ShowLineNumbers ?? DefaultShowLineNumbers;

    public bool EffectiveWordWrap => WordWrap ?? DefaultWordWrap;

    public string EffectiveSaveKeybinding => SaveKeybinding ?? DefaultSaveKeybinding;

    public string EffectiveOpenSettingsKeybinding => OpenSettingsKeybinding ?? DefaultOpenSettingsKeybinding;
}
