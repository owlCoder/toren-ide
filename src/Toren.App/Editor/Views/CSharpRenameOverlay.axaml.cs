using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Toren.App.Editor.Views;

internal sealed partial class CSharpRenameOverlay : UserControl
{
    public CSharpRenameOverlay()
    {
        InitializeComponent();
    }

    public event EventHandler? RenameRequested;

    public event EventHandler? CloseRequested;

    public string NewName => NameBox.Text?.Trim() ?? string.Empty;

    public void ShowOverlay(string currentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentName);
        NameBox.Text = currentName;
        HintText.Text = "Enter to rename · Esc to cancel";
        IsVisible = true;
        NameBox.Focus();
        NameBox.SelectAll();
    }

    public void SetError(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        HintText.Text = message;
    }

    public void HideOverlay()
    {
        IsVisible = false;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void NameBox_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.Escape)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
            eventArgs.Handled = true;
            return;
        }

        if (eventArgs.Key == Key.Enter)
        {
            RequestRename();
            eventArgs.Handled = true;
        }
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RenameButton_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        RequestRename();
    }

    private void RequestRename()
    {
        if (string.IsNullOrWhiteSpace(NewName))
        {
            SetError("Enter a valid symbol name.");
            return;
        }

        RenameRequested?.Invoke(this, EventArgs.Empty);
    }
}
