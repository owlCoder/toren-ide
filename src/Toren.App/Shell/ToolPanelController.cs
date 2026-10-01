using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Toren.App.Shell;

internal sealed class ToolPanelController
{
    private static readonly ConditionalWeakTable<Window, ToolPanelController> Instances = new();
    private readonly Window _window;
    private readonly Grid _grid;
    private readonly Control _panel;
    private readonly Control _documents;
    private readonly GridSplitter _splitter;
    private readonly TabControl _tabs;
    private readonly Button _expand;
    private readonly Button _hide;
    private readonly Button _toggle;
    private GridLength _panelHeight;
    private GridLength _documentHeight;
    private double _documentMinimum;

    private ToolPanelController(Window window)
    {
        _window = window;
        _grid = window.FindControl<Grid>("ShellGrid")!;
        _panel = window.FindControl<Border>("ToolPanel")!;
        _documents = window.FindControl<Grid>("DocumentHost")!;
        _splitter = window.FindControl<GridSplitter>("ToolPanelSplitter")!;
        _tabs = window.FindControl<TabControl>("ToolTabs")!;
        _expand = window.FindControl<Button>("ExpandToolPanelButton")!;
        _hide = window.FindControl<Button>("HideToolPanelButton")!;
        _toggle = window.FindControl<Button>("ToggleToolPanelButton")!;
        _panelHeight = _grid.RowDefinitions[3].Height;
        _expand.Click += Expand_OnClick;
        _hide.Click += Hide_OnClick;
        _toggle.Click += Toggle_OnClick;
        _tabs.SelectionChanged += Tabs_OnSelectionChanged;
        window.AddHandler(InputElement.KeyDownEvent, Window_OnKeyDown, RoutingStrategies.Tunnel);
        window.Closed += Window_OnClosed;
    }

    public bool IsVisible => _panel.IsVisible;
    public bool IsExpanded { get; private set; }

    public static ToolPanelController For(Window window) => Instances.GetValue(window, static owner => new(owner));

    public void Show()
    {
        if (IsVisible) return;
        _panel.IsVisible = true;
        _splitter.IsVisible = true;
        _grid.RowDefinitions[3].MinHeight = 300;
        _grid.RowDefinitions[3].Height = _panelHeight;
    }

    public void Hide()
    {
        if (!IsVisible) return;
        if (IsExpanded) Restore();
        _panelHeight = _grid.RowDefinitions[3].Height;
        _panel.IsVisible = false;
        _splitter.IsVisible = false;
        _grid.RowDefinitions[3].MinHeight = 0;
        _grid.RowDefinitions[3].Height = new GridLength(0);
        _window.FindControl<AvaloniaEdit.TextEditor>("DocumentEditor")?.Focus();
    }

    public void ToggleExpanded()
    {
        Show();
        if (IsExpanded)
        {
            Restore();
            return;
        }

        _panelHeight = _grid.RowDefinitions[3].Height;
        _documentHeight = _grid.RowDefinitions[2].Height;
        _documentMinimum = _grid.RowDefinitions[2].MinHeight;
        _grid.RowDefinitions[2].MinHeight = 0;
        _grid.RowDefinitions[2].Height = new GridLength(0);
        _grid.RowDefinitions[3].Height = new GridLength(1, GridUnitType.Star);
        _documents.IsVisible = false;
        _splitter.IsVisible = false;
        IsExpanded = true;
        UpdateExpandButton();
    }

    private void Restore()
    {
        _grid.RowDefinitions[2].MinHeight = _documentMinimum;
        _grid.RowDefinitions[2].Height = _documentHeight;
        _grid.RowDefinitions[3].Height = _panelHeight;
        _documents.IsVisible = true;
        _splitter.IsVisible = true;
        IsExpanded = false;
        UpdateExpandButton();
    }

    private void UpdateExpandButton()
    {
        var label = IsExpanded ? "Restore panel size" : "Maximize panel";
        ToolTip.SetTip(_expand, label);
        Avalonia.Automation.AutomationProperties.SetName(_expand, label);
        if (_expand.Content is Avalonia.Controls.Shapes.Path icon
            && _window.TryFindResource(IsExpanded ? "TorenIconChevronDown" : "TorenIconChevronUp", out var geometry))
        {
            icon.Data = (Geometry)geometry!;
        }
    }

    private void Expand_OnClick(object? sender, RoutedEventArgs args) => ToggleExpanded();
    private void Hide_OnClick(object? sender, RoutedEventArgs args) => Hide();
    private void Toggle_OnClick(object? sender, RoutedEventArgs args)
    {
        if (IsVisible) Hide(); else Show();
    }

    private void Tabs_OnSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (ReferenceEquals(args.Source, _tabs)) Show();
    }

    private void Window_OnKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key != Key.J || args.KeyModifiers is not (KeyModifiers.Control or KeyModifiers.Meta)) return;
        if (IsVisible) Hide(); else Show();
        args.Handled = true;
    }

    private void Window_OnClosed(object? sender, EventArgs args)
    {
        _expand.Click -= Expand_OnClick;
        _hide.Click -= Hide_OnClick;
        _toggle.Click -= Toggle_OnClick;
        _tabs.SelectionChanged -= Tabs_OnSelectionChanged;
        _window.RemoveHandler(InputElement.KeyDownEvent, Window_OnKeyDown);
        _window.Closed -= Window_OnClosed;
    }
}
