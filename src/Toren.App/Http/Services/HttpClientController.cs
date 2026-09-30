using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Toren.App.Http.ViewModels;
using Toren.App.ViewModels;
using Toren.App.Views.Http;

namespace Toren.App.Http.Services;

internal sealed class HttpClientController
{
    private readonly Window _window;
    private readonly MainWindowViewModel _shell;
    private readonly HttpClientViewModel _viewModel;
    private readonly TabControl _toolTabs;
    private readonly TabItem _httpTab;
    private OpenDocumentViewModel? _activeDocument;
    private bool _detached;

    private HttpClientController(
        Window window,
        MainWindowViewModel shell,
        HttpClientViewModel viewModel,
        TabControl toolTabs)
    {
        _window = window;
        _shell = shell;
        _viewModel = viewModel;
        _toolTabs = toolTabs;
        _httpTab = new TabItem
        {
            Header = "HTTP",
            Content = new HttpClientPanel { DataContext = viewModel },
        };
        _toolTabs.Items.Add(_httpTab);

        _shell.Documents.PropertyChanged += Documents_OnPropertyChanged;
        _window.Closed += Window_OnClosed;
        SynchronizeActiveDocument();
    }

    public static void Attach(Window window, MainWindowViewModel shell, HttpClientViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(viewModel);

        var toolTabs = window.GetLogicalDescendants()
            .OfType<TabControl>()
            .FirstOrDefault(control => control.Classes.Contains("tool-tabs"));
        if (toolTabs is null)
        {
            return;
        }

        _ = new HttpClientController(window, shell, viewModel, toolTabs);
    }

    private void Documents_OnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(DocumentHostViewModel.ActiveDocument))
        {
            SynchronizeActiveDocument();
        }
    }

    private void ActiveDocument_OnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(OpenDocumentViewModel.Text))
        {
            SynchronizeDocumentContent();
        }
    }

    private void SynchronizeActiveDocument()
    {
        _viewModel.Cancel();
        if (_activeDocument is not null)
        {
            _activeDocument.PropertyChanged -= ActiveDocument_OnPropertyChanged;
        }

        _activeDocument = _shell.Documents.ActiveDocument;
        if (_activeDocument is not null)
        {
            _activeDocument.PropertyChanged += ActiveDocument_OnPropertyChanged;
        }

        SynchronizeDocumentContent();
    }

    private void SynchronizeDocumentContent()
    {
        if (_detached || _activeDocument is null)
        {
            _viewModel.SetDocument(null, null);
            return;
        }

        _viewModel.SetDocument(_activeDocument.Path, _activeDocument.Text);
    }

    private void Window_OnClosed(object? sender, EventArgs eventArgs) => Detach();

    private void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        _viewModel.Cancel();
        if (_activeDocument is not null)
        {
            _activeDocument.PropertyChanged -= ActiveDocument_OnPropertyChanged;
        }

        _shell.Documents.PropertyChanged -= Documents_OnPropertyChanged;
        _window.Closed -= Window_OnClosed;
        _toolTabs.Items.Remove(_httpTab);
    }
}
