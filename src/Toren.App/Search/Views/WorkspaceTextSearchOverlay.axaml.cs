using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Toren.App.Search.Models;

namespace Toren.App.Search.Views;

internal sealed partial class WorkspaceTextSearchOverlay : UserControl
{
    public WorkspaceTextSearchOverlay()
    {
        InitializeComponent();
    }

    public event EventHandler? QueryChanged;

    public event EventHandler? SearchOptionsChanged;

    public event EventHandler? ResultRequested;

    public event EventHandler? CloseRequested;

    public string Query => QueryBox.Text ?? string.Empty;

    public bool MatchCase => MatchCaseButton.IsChecked == true;

    public bool MatchWholeWord => MatchWholeWordButton.IsChecked == true;

    public bool UseRegularExpression => UseRegularExpressionButton.IsChecked == true;

    public string IncludePatterns => IncludePatternsBox.Text ?? string.Empty;

    public string ExcludePatterns => ExcludePatternsBox.Text ?? string.Empty;

    public WorkspaceTextSearchResult? SelectedResult => ResultsList.SelectedItem as WorkspaceTextSearchResult;

    public void ShowOverlay()
    {
        IsVisible = true;
        QueryBox.Focus();
        QueryBox.SelectAll();
    }

    public void HideOverlay()
    {
        IsVisible = false;
    }

    public void SetResults(IReadOnlyList<WorkspaceTextSearchResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        ResultsList.ItemsSource = results;
        ResultsList.SelectedIndex = results.Count > 0 ? 0 : -1;
    }

    public void SetStatus(string status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        StatusText.Text = status;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void QueryBox_OnTextChanged(object? sender, TextChangedEventArgs eventArgs)
    {
        QueryChanged?.Invoke(this, EventArgs.Empty);
    }

    private void FilterBox_OnTextChanged(object? sender, TextChangedEventArgs eventArgs)
    {
        SearchOptionsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SearchOptionButton_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        SearchOptionsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void QueryBox_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (TryClose(eventArgs))
        {
            return;
        }

        if (eventArgs.Key == Key.Enter)
        {
            RequestSelectedResult();
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

    private void FilterBox_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        _ = TryClose(eventArgs);
    }

    private bool TryClose(KeyEventArgs eventArgs)
    {
        if (eventArgs.Key != Key.Escape)
        {
            return false;
        }

        CloseRequested?.Invoke(this, EventArgs.Empty);
        eventArgs.Handled = true;
        return true;
    }

    private void ResultsList_OnDoubleTapped(object? sender, TappedEventArgs eventArgs)
    {
        RequestSelectedResult();
        eventArgs.Handled = true;
    }

    private void RequestSelectedResult()
    {
        if (SelectedResult is not null)
        {
            ResultRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
