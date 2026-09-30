using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Toren.App.Search.Models;

namespace Toren.App.Search.Views;

internal sealed partial class WorkspaceTextSearchOverlay : UserControl
{
    private IReadOnlyList<WorkspaceTextSearchDisplayItem> _displayItems = [];

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

    public WorkspaceTextSearchResult? SelectedResult =>
        (ResultsList.SelectedItem as WorkspaceTextSearchDisplayItem)?.Result;

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
        _displayItems = WorkspaceTextSearchPresentation.Build(results);
        ResultsList.ItemsSource = _displayItems;
        ResultsList.SelectedIndex = FindResultIndex(startIndex: 0, delta: 1);
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

        if (eventArgs.Key is not Key.Down and not Key.Up || _displayItems.Count == 0)
        {
            return;
        }

        var delta = eventArgs.Key == Key.Down ? 1 : -1;
        var startIndex = ResultsList.SelectedIndex < 0
            ? (delta > 0 ? 0 : _displayItems.Count - 1)
            : ResultsList.SelectedIndex + delta;
        var nextIndex = FindResultIndex(startIndex, delta);
        if (nextIndex >= 0)
        {
            ResultsList.SelectedIndex = nextIndex;
            ResultsList.ScrollIntoView(nextIndex);
        }

        eventArgs.Handled = true;
    }

    private int FindResultIndex(int startIndex, int delta)
    {
        for (var index = startIndex; index >= 0 && index < _displayItems.Count; index += delta)
        {
            if (_displayItems[index].IsResult)
            {
                return index;
            }
        }

        return -1;
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
