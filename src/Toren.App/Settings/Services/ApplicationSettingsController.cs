using System.ComponentModel;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using AvaloniaEdit;
using Toren.App.Settings.Contracts;
using Toren.App.Settings.Models;
using Toren.App.Settings.ViewModels;
using Toren.App.Views.Settings;

namespace Toren.App.Settings.Services;

internal sealed class ApplicationSettingsController
{
    private readonly Window _window;
    private readonly IApplicationSettingsStore _store;
    private readonly ApplicationSettingsViewModel _viewModel;
    private readonly TextEditor _editor;
    private readonly Action<ApplicationThemePreference> _applyTheme;
    private readonly Func<Task> _saveActiveDocument;
    private readonly TabControl _toolTabs;
    private readonly TabItem _settingsTab;
    private readonly ApplicationSettingsPanel _settingsPanel;
    private readonly Button? _settingsButton;
    private CancellationTokenSource? _saveCancellation;
    private bool _loading;
    private bool _detached;

    private ApplicationSettingsController(
        Window window,
        IApplicationSettingsStore store,
        ApplicationSettingsViewModel viewModel,
        TextEditor editor,
        Action<ApplicationThemePreference> applyTheme,
        Func<Task> saveActiveDocument,
        TabControl toolTabs)
    {
        _window = window;
        _store = store;
        _viewModel = viewModel;
        _editor = editor;
        _applyTheme = applyTheme;
        _saveActiveDocument = saveActiveDocument;
        _toolTabs = toolTabs;
        _settingsPanel = new ApplicationSettingsPanel { DataContext = viewModel };
        _settingsTab = new TabItem
        {
            Header = "SETTINGS",
            Content = _settingsPanel,
        };
        AutomationProperties.SetName(_settingsTab, "Settings tool tab");
        _toolTabs.Items.Add(_settingsTab);

        _settingsButton = window.FindControl<Button>("SettingsActivityButton");
        if (_settingsButton is not null)
        {
            _settingsButton.IsEnabled = true;
            ToolTip.SetTip(_settingsButton, "Settings");
            AutomationProperties.SetName(_settingsButton, "Settings");
            AutomationProperties.SetHelpText(
                _settingsButton,
                "Open application settings and keyboard shortcuts");
            _settingsButton.Click += SettingsButton_OnClick;
        }

        _viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
        _window.KeyDown += Window_OnKeyDown;
        _window.Opened += Window_OnOpened;
        _window.Closed += Window_OnClosed;
    }

    public static void Attach(
        Window window,
        IApplicationSettingsStore store,
        ApplicationSettingsViewModel viewModel,
        TextEditor editor,
        Action<ApplicationThemePreference> applyTheme,
        Func<Task> saveActiveDocument)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(applyTheme);
        ArgumentNullException.ThrowIfNull(saveActiveDocument);

        var toolTabs = window.FindControl<TabControl>("ToolTabs");
        if (toolTabs is not null)
        {
            _ = new ApplicationSettingsController(
                window,
                store,
                viewModel,
                editor,
                applyTheme,
                saveActiveDocument,
                toolTabs);
        }
    }

    private async void Window_OnOpened(object? sender, EventArgs eventArgs)
    {
        var result = await _store.LoadAsync().ConfigureAwait(true);
        if (result.IsFailure)
        {
            _viewModel.StatusText = result.Error.Message;
            return;
        }

        _loading = true;
        try
        {
            _viewModel.Load(result.Value!);
            ApplySettings(_viewModel.ToSettings());
            _viewModel.StatusText = "Settings loaded.";
        }
        finally
        {
            _loading = false;
        }
    }

    private async void Window_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Handled)
        {
            return;
        }

        var settings = _viewModel.ToSettings();
        if (ApplicationKeybindingMatcher.Matches(
                settings.EffectiveOpenSettingsKeybinding,
                eventArgs.Key,
                eventArgs.KeyModifiers))
        {
            OpenSettings();
            eventArgs.Handled = true;
            return;
        }

        if (!ApplicationKeybindingMatcher.Matches(
                settings.EffectiveSaveKeybinding,
                eventArgs.Key,
                eventArgs.KeyModifiers))
        {
            return;
        }

        await _saveActiveDocument().ConfigureAwait(true);
        eventArgs.Handled = true;
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (_loading
            || eventArgs.PropertyName is nameof(ApplicationSettingsViewModel.SearchText)
                or nameof(ApplicationSettingsViewModel.StatusText)
                or nameof(ApplicationSettingsViewModel.ShowAppearanceSection)
                or nameof(ApplicationSettingsViewModel.ShowEditorSection)
                or nameof(ApplicationSettingsViewModel.ShowKeyboardSection)
                or nameof(ApplicationSettingsViewModel.HasSearchResults))
        {
            return;
        }

        var settings = _viewModel.ToSettings();
        ApplySettings(settings);
        QueueSave(settings);
    }

    private void ApplySettings(ApplicationSettings settings)
    {
        _applyTheme(settings.Theme);
        _editor.FontSize = settings.EffectiveEditorFontSize;
        _editor.ShowLineNumbers = settings.EffectiveShowLineNumbers;
        _editor.WordWrap = settings.EffectiveWordWrap;
    }

    private void QueueSave(ApplicationSettings settings)
    {
        CancelSave();
        var cancellation = new CancellationTokenSource();
        _saveCancellation = cancellation;
        _ = SaveAsync(settings, cancellation);
    }

    private async Task SaveAsync(
        ApplicationSettings settings,
        CancellationTokenSource cancellation)
    {
        try
        {
            var result = await _store.SaveAsync(settings, cancellation.Token).ConfigureAwait(true);
            if (ReferenceEquals(_saveCancellation, cancellation) && result.IsSuccess)
            {
                _viewModel.StatusText = "Settings saved.";
            }
            else if (ReferenceEquals(_saveCancellation, cancellation) && result.IsFailure)
            {
                _viewModel.StatusText = result.Error.Message;
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            if (ReferenceEquals(_saveCancellation, cancellation))
            {
                _saveCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private void SettingsButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs) =>
        OpenSettings();

    private void OpenSettings()
    {
        _toolTabs.SelectedItem = _settingsTab;
        Dispatcher.UIThread.Post(_settingsPanel.FocusSearch, DispatcherPriority.Input);
    }

    private void CancelSave()
    {
        var cancellation = Interlocked.Exchange(ref _saveCancellation, null);
        cancellation?.Cancel();
    }

    private void Window_OnClosed(object? sender, EventArgs eventArgs) => Detach();

    private void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        CancelSave();
        _viewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        _window.KeyDown -= Window_OnKeyDown;
        _window.Opened -= Window_OnOpened;
        _window.Closed -= Window_OnClosed;
        if (_settingsButton is not null)
        {
            _settingsButton.Click -= SettingsButton_OnClick;
        }

        _toolTabs.Items.Remove(_settingsTab);
    }
}
