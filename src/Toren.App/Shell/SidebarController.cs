using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Toren.App.Shell;

internal sealed class SidebarController
{
    private static readonly ConditionalWeakTable<Window, SidebarController> Instances = new();
    private readonly Border _host;
    private readonly Dictionary<string, SidebarItem> _items = new();
    private SidebarItem? _selected;

    private SidebarController(Window window)
    {
        _host = window.FindControl<Border>("ExplorerPanel")
            ?? throw new InvalidOperationException("The sidebar could not be located.");
        var explorer = _host.Child!;
        Register(window, "ExplorerActivityButton", explorer);
        Show("ExplorerActivityButton");
        window.Closed += (_, _) =>
        {
            foreach (var item in _items.Values)
            {
                item.Button.Click -= item.Click;
            }
        };
    }

    public Control? ActiveContent => _host.Child;

    public static SidebarController For(Window window) => Instances.GetValue(window, static owner => new(owner));

    public void Register(Window window, string buttonName, Control content, Action? onHidden = null)
    {
        var button = window.FindControl<Button>(buttonName)
            ?? throw new InvalidOperationException($"The {buttonName} activity button could not be located.");
        EventHandler<RoutedEventArgs> click = (_, _) => Show(buttonName);
        _items.Add(buttonName, new SidebarItem(button, content, click, onHidden));
        button.IsEnabled = true;
        button.Click += click;
    }

    public void Show(string buttonName)
    {
        var next = _items[buttonName];
        if (ReferenceEquals(next, _selected))
        {
            return;
        }

        _selected?.OnHidden?.Invoke();
        foreach (var item in _items.Values)
        {
            item.Button.Classes.Remove("active");
        }

        next.Button.Classes.Add("active");
        _host.Child = next.Content;
        _selected = next;
    }

    private sealed record SidebarItem(Button Button, Control Content, EventHandler<RoutedEventArgs> Click, Action? OnHidden);
}
