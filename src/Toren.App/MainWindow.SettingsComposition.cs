using Toren.App.Settings.Adapters;
using Toren.App.ViewModels;

namespace Toren.App;

internal sealed partial class MainWindow
{
    public MainWindow(MainWindowViewModel viewModel)
        : this(viewModel, CreateApplicationSettingsStore())
    {
    }

    private static FileApplicationSettingsStore CreateApplicationSettingsStore()
    {
        var applicationDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TorenIDE");
        return new FileApplicationSettingsStore(
            Path.Combine(applicationDataDirectory, "settings.json"));
    }
}
