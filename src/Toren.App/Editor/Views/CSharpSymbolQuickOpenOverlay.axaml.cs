using Avalonia.Controls;
using Avalonia.Input;
using Toren.Language.CSharp.Models;

namespace Toren.App.Editor.Views;

internal sealed partial class CSharpSymbolQuickOpenOverlay : UserControl
{
    public CSharpSymbolQuickOpenOverlay()
    {
        InitializeComponent();
    }

    public event EventHandler? QueryChanged;

    public event EventHandler? SymbolRequested;

    public event EventHandler? CloseRequested;

    public string Query => QueryBox.Text ?? string.Empty;

    public CSharpWorkspaceSymbol? SelectedSymbol => ResultsList.SelectedItem as CSharpWorkspaceSymbol;

    public void ShowOverlay()
    {
        IsVisible = true;
        QueryBox.Text = string.Empty;
        QueryBox.Focus();
    }

    public void HideOverlay()
    {
        IsVisible = false;
    }

    public void SetResults(IReadOnlyList<CSharpWorkspaceSymbol> symbols)
    {
        ArgumentNullException.ThrowIfNull(symbols);
        ResultsList.ItemsSource = symbols;
        ResultsList.SelectedIndex = symbols.Count > 0 ? 0 : -1;
    }

    public void SetStatus(string status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        StatusText.Text = status;
    }

    private void QueryBox_OnTextChanged(object? sender, TextChangedEventArgs eventArgs)
    {
        QueryChanged?.Invoke(this, EventArgs.Empty);
    }

    private void QueryBox_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.Escape)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
            eventArgs.Handled = true;
            return;
        }

        if (eventArgs.Key == Key.Enter)
        {
            RequestSelectedSymbol();
            eventArgs.Handled = true;
            return;
        }

        if (eventArgs.Key is not Key.Down and not Key.Up || ResultsList.ItemCount == 0)
        {
            return;
        }

        var delta = eventArgs.Key == Key.Down ? 1 : -1;
        var current = Math.Max(ResultsList.SelectedIndex, 0);
        ResultsList.SelectedIndex = Math.Clamp(current + delta, 0, ResultsList.ItemCount - 1);
        eventArgs.Handled = true;
    }

    private void ResultsList_OnDoubleTapped(object? sender, TappedEventArgs eventArgs)
    {
        RequestSelectedSymbol();
        eventArgs.Handled = true;
    }

    private void RequestSelectedSymbol()
    {
        if (SelectedSymbol is not null)
        {
            SymbolRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
