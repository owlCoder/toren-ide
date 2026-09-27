using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

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

    public event EventHandler? GoToLineRequested;

    public event EventHandler? CloseRequested;

    public EditorNavigationMode Mode { get; private set; } = EditorNavigationMode.Find;

    public string Query => InputBox.Text ?? string.Empty;

    public bool MatchCase => MatchCaseButton.IsChecked == true;

    public void ShowFind(string initialQuery)
    {
        Mode = EditorNavigationMode.Find;
        ModeLabel.Text = "Find";
        InputBox.Watermark = "Find in file";
        InputBox.Width = 220;
        MatchStatus.IsVisible = true;
        MatchCaseButton.IsVisible = true;
        PreviousButton.IsVisible = true;
        NextButton.IsVisible = true;
        IsVisible = true;
        InputBox.Text = initialQuery;
        InputBox.Focus();
        InputBox.SelectAll();
    }

    public void ShowGoToLine(int currentLine)
    {
        Mode = EditorNavigationMode.GoToLine;
        ModeLabel.Text = "Ln";
        InputBox.Watermark = "Go to line";
        InputBox.Width = 140;
        MatchStatus.IsVisible = false;
        MatchCaseButton.IsVisible = false;
        PreviousButton.IsVisible = false;
        NextButton.IsVisible = false;
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

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void InputBox_OnTextChanged(object? sender, TextChangedEventArgs eventArgs)
    {
        if (Mode == EditorNavigationMode.Find)
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

    private void CloseButton_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}

internal enum EditorNavigationMode
{
    Find,
    GoToLine,
}
