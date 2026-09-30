using Toren.App.Documents.Adapters;
using Toren.App.Documents.Contracts;
using Toren.App.Documents.Services;
using Toren.App.Settings.Adapters;
using Toren.App.Settings.Contracts;
using Toren.App.Settings.Models;
using Toren.App.Settings.Services;
using Toren.App.Settings.ViewModels;
using Toren.App.ViewModels;

namespace Toren.App;

internal sealed partial class MainWindow
{
    public MainWindow(MainWindowViewModel viewModel)
        : this(viewModel, CreateApplicationSettingsStore(), CreateDocumentSessionStore())
    {
    }

    public MainWindow(
        MainWindowViewModel viewModel,
        IApplicationSettingsStore applicationSettingsStore,
        IDocumentSessionStore documentSessionStore)
        : this(viewModel, applicationSettingsStore)
    {
        KeyDown -= MainWindow_OnKeyDown;
        DocumentSessionRecoveryController.Attach(
            this,
            _viewModel.Documents,
            documentSessionStore,
            _viewModel.SetStatus);
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

    private static FileApplicationSettingsStore CreateApplicationSettingsStore() =>
        new(Path.Combine(GetApplicationDataDirectory(), "settings.json"));

    private static FileDocumentSessionStore CreateDocumentSessionStore() =>
        new(Path.Combine(GetApplicationDataDirectory(), "document-session.json"));

    private static string GetApplicationDataDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TorenIDE");
}
