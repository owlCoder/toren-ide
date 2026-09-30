using Avalonia.Input;

namespace Toren.App.Settings.Services;

public static class ApplicationKeybindingMatcher
{
    public static bool Matches(string gesture, Key key, KeyModifiers modifiers)
    {
        var primary = modifiers.HasFlag(KeyModifiers.Control)
            || modifiers.HasFlag(KeyModifiers.Meta);
        var shift = modifiers.HasFlag(KeyModifiers.Shift);
        var alt = modifiers.HasFlag(KeyModifiers.Alt);

        return gesture switch
        {
            "primary+s" => key == Key.S && primary && !shift && !alt,
            "primary+shift+s" => key == Key.S && primary && shift && !alt,
            "alt+s" => key == Key.S && !primary && !shift && alt,
            "primary+alt+s" => key == Key.S && primary && !shift && alt,
            "primary+alt+shift+s" => key == Key.S && primary && shift && alt,
            "f12" => key == Key.F12 && !primary && !shift && !alt,
            _ => false,
        };
    }
}
