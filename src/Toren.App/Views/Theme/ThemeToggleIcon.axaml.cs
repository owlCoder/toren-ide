using Avalonia.Controls;

namespace Toren.App.Views.Theme;

internal sealed partial class ThemeToggleIcon : UserControl
{
    public ThemeToggleIcon()
    {
        InitializeComponent();
    }

    public void SetDarkMode(bool isDark)
    {
        SunIcon.IsVisible = isDark;
        MoonIcon.IsVisible = !isDark;
    }
}
