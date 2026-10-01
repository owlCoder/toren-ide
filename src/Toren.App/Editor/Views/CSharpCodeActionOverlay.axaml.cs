using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Toren.Language.CSharp.Models;

namespace Toren.App.Editor.Views;

internal sealed partial class CSharpCodeActionOverlay : UserControl
{
    public CSharpCodeActionOverlay()
    {
        InitializeComponent();
        ResultsList.AddHandler(KeyDownEvent, ResultsList_OnKeyDown, RoutingStrategies.Tunnel);
    }

    public event EventHandler? ActionRequested;

    public event EventHandler? CloseRequested;

    public CSharpCodeActionInfo? SelectedAction => ResultsList.SelectedItem as CSharpCodeActionInfo;

    public void ShowOverlay(IReadOnlyList<CSharpCodeActionInfo> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        ResultsList.ItemsSource = actions;
        ResultsList.SelectedIndex = actions.Count > 0 ? 0 : -1;
        IsVisible = actions.Count > 0;
        if (IsVisible)
        {
            OverlayFocus.Schedule(this, ResultsList);
        }
    }

    public void HideOverlay()
    {
        IsVisible = false;
        ResultsList.ItemsSource = null;
    }

    private void ResultsList_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.Escape)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
            eventArgs.Handled = true;
            return;
        }

        if (eventArgs.Key == Key.Enter)
        {
            RequestSelectedAction();
            eventArgs.Handled = true;
        }
    }

    private void ResultsList_OnDoubleTapped(object? sender, TappedEventArgs eventArgs)
    {
        RequestSelectedAction();
        eventArgs.Handled = true;
    }

    private void RequestSelectedAction()
    {
        if (SelectedAction is not null)
        {
            ActionRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
