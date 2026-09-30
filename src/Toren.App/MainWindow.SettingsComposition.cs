using Toren.App.Settings.Adapters;
using Toren.App.Settings.Models;
using Toren.App.Settings.Services;
using Toren.App.Settings.ViewModels;
using Toren.App.ViewModels;

namespace Toren.App;

internal sealed partial class MainWindow
{
    public MainWindow(MainWindowViewModel viewModel)
        : this(viewModel, CreateApplicationSettingsStore())
    {
        KeyDown -= MainWindow_OnKeyDown;
        ApplicationSettingsController.Attach(
            this,
            _applicationSettingsStore,
            new ApplicationSettingsViewModel(),
            DocumentEditor,
            ApplyThemePreference,
            () => _viewModel.SaveActiveDocumentAsync());
    }

    private void ApplyThemePreference(ApplicationThemePreference theme) =>
        ApplyTheme(theme == ApplicationThemePreference.Dark);

    private static FileApplicationSettingsStore CreateApplicationSettingsStore()
    {
        var applicationDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TorenIDE");
        return new FileApplicationSettingsStore(
            Path.Combine(applicationDataDirectory, "settings.json"));
    }
}
