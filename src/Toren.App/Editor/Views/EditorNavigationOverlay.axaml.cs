using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Toren.App.Editor.Views;

internal sealed partial class EditorNavigationOverlay : UserControl
{
    public EditorNavigationOverlay()
    {
        InitializeComponent();
    }

    public event EventHandler? QueryChanged;

    public event EventHandler? NextRequested;

    public event EventHandler? PreviousRequested;

    public event EventHandler? ReplaceNextRequested;

    public event EventHandler? ReplaceAllRequested;

    public event EventHandler? GoToLineRequested;

    public event EventHandler? CloseRequested;

    public EditorNavigationMode Mode { get; private set; } = EditorNavigationMode.Find;

    public string Query => InputBox.Text ?? string.Empty;

    public string Replacement => ReplacementBox.Text ?? string.Empty;

    public bool MatchCase => MatchCaseButton.IsChecked == true;

    public void ShowFind(string initialQuery)
    {
        ConfigureSearchMode(EditorNavigationMode.Find, "Find", initialQuery, showReplacement: false);
    }

    public void ShowReplace(string initialQuery)
    {
        ConfigureSearchMode(EditorNavigationMode.Replace, "Replace", initialQuery, showReplacement: true);
    }

    public void ShowGoToLine(int currentLine)
    {
        Mode = EditorNavigationMode.GoToLine;
        ModeLabel.Text = "Ln";
        InputBox.PlaceholderText = "Go to line";
        InputBox.Width = 140;
        MatchStatus.IsVisible = false;
        MatchCaseButton.IsVisible = false;
        PreviousButton.IsVisible = false;
        NextButton.IsVisible = false;
        ReplacementRow.IsVisible = false;
        IsVisible = true;
        InputBox.Text = currentLine.ToString(System.Globalization.CultureInfo.InvariantCulture);
        InputBox.Focus();
        InputBox.SelectAll();
    }

    public void HideOverlay()
    {
        IsVisible = false;
    }

    public void SetMatchStatus(int current, int total)
    {
        MatchStatus.Text = total == 0
            ? "0/0"
            : $"{current}/{total}";
    }

    private void ConfigureSearchMode(
        EditorNavigationMode mode,
        string label,
        string initialQuery,
        bool showReplacement)
    {
        Mode = mode;
        ModeLabel.Text = label;
        InputBox.PlaceholderText = "Find in file";
        InputBox.Width = 220;
        MatchStatus.IsVisible = true;
        MatchCaseButton.IsVisible = true;
        PreviousButton.IsVisible = true;
        NextButton.IsVisible = true;
        ReplacementRow.IsVisible = showReplacement;
        IsVisible = true;
        InputBox.Text = initialQuery;
        InputBox.Focus();
        InputBox.SelectAll();
    }

    private void InputBox_OnTextChanged(object? sender, TextChangedEventArgs eventArgs)
    {
        if (Mode is EditorNavigationMode.Find or EditorNavigationMode.Replace)
        {
            QueryChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void InputBox_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.Escape)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
            eventArgs.Handled = true;
            return;
        }

        if (eventArgs.Key != Key.Enter)
        {
            return;
        }

        if (Mode == EditorNavigationMode.GoToLine)
        {
            GoToLineRequested?.Invoke(this, EventArgs.Empty);
        }
        else if (eventArgs.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            PreviousRequested?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            NextRequested?.Invoke(this, EventArgs.Empty);
        }

        eventArgs.Handled = true;
    }

    private void ReplacementBox_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.Escape)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
            eventArgs.Handled = true;
        }
        else if (eventArgs.Key == Key.Enter)
        {
            ReplaceNextRequested?.Invoke(this, EventArgs.Empty);
            eventArgs.Handled = true;
        }
    }

    private void MatchCaseButton_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        QueryChanged?.Invoke(this, EventArgs.Empty);
    }

    private void PreviousButton_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        PreviousRequested?.Invoke(this, EventArgs.Empty);
    }

    private void NextButton_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        NextRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ReplaceNextButton_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        ReplaceNextRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ReplaceAllButton_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        ReplaceAllRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CloseButton_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}

internal enum EditorNavigationMode
{
    Find,
    Replace,
    GoToLine,
}
