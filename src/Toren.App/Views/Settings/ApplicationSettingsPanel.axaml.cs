using Avalonia.Controls;

namespace Toren.App.Views.Settings;

internal sealed partial class ApplicationSettingsPanel : UserControl
{
    public ApplicationSettingsPanel()
    {
        InitializeComponent();
    }

    public void FocusSearch() => SettingsSearchBox.Focus();
}
