using Avalonia.Controls;
using Avalonia.Input;
using Toren.Workspaces.Models;

namespace Toren.App.Editor.Views;

internal sealed partial class WorkspaceQuickOpenOverlay : UserControl
{
    public WorkspaceQuickOpenOverlay()
    {
        InitializeComponent();
    }

    public event EventHandler? QueryChanged;

    public event EventHandler? FileRequested;

    public event EventHandler? CloseRequested;

    public string Query => QueryBox.Text ?? string.Empty;

    public WorkspaceFileEntry? SelectedFile => ResultsList.SelectedItem as WorkspaceFileEntry;

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

    public void SetResults(IReadOnlyList<WorkspaceFileEntry> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        ResultsList.ItemsSource = files;
        ResultsList.SelectedIndex = files.Count > 0 ? 0 : -1;
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
            RequestSelectedFile();
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
        RequestSelectedFile();
        eventArgs.Handled = true;
    }

    private void RequestSelectedFile()
    {
        if (SelectedFile is not null)
        {
            FileRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
