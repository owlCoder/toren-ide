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
    bool? WordWrap = null)
{
    public const double DefaultEditorFontSize = 13d;
    public const bool DefaultShowLineNumbers = true;
    public const bool DefaultWordWrap = false;

    public static ApplicationSettings Default { get; } = new(
        ApplicationThemePreference.Dark,
        DefaultEditorFontSize,
        DefaultShowLineNumbers,
        DefaultWordWrap);

    public double EffectiveEditorFontSize => EditorFontSize ?? DefaultEditorFontSize;

    public bool EffectiveShowLineNumbers => ShowLineNumbers ?? DefaultShowLineNumbers;

    public bool EffectiveWordWrap => WordWrap ?? DefaultWordWrap;
}
