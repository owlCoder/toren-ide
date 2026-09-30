namespace Toren.App.Settings.Models;

public enum ApplicationThemePreference
{
    Dark = 0,
    Light = 1,
}

public sealed record ApplicationSettings(ApplicationThemePreference Theme)
{
    public static ApplicationSettings Default { get; } = new(ApplicationThemePreference.Dark);
}
