using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using AvaloniaEdit;
using Toren.App.Editor.Models;
using Toren.App.Editor.Views;

namespace Toren.App.Editor.Services;

internal sealed class EditorSearchController
{
    private const int MaxSelectionQueryLength = 120;

    private readonly Window _window;
    private readonly TextEditor _editor;
    private readonly Grid _host;
    private readonly EditorNavigationOverlay _overlay;
    private IReadOnlyList<TextSearchMatch> _matches = [];
    private int _selectedMatchIndex = -1;
    private bool _detached;

    private EditorSearchController(Window window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _editor = window.FindControl<TextEditor>("DocumentEditor")
            ?? throw new InvalidOperationException("The document editor could not be located.");
        _host = _editor.Parent as Grid
            ?? throw new InvalidOperationException("The document editor must be hosted by a Grid.");
        _overlay = new EditorNavigationOverlay();
        _host.Children.Add(_overlay);

        _overlay.QueryChanged += Overlay_OnQueryChanged;
        _overlay.NextRequested += Overlay_OnNextRequested;
        _overlay.PreviousRequested += Overlay_OnPreviousRequested;
        _overlay.ReplaceNextRequested += Overlay_OnReplaceNextRequested;
        _overlay.ReplaceAllRequested += Overlay_OnReplaceAllRequested;
        _overlay.GoToLineRequested += Overlay_OnGoToLineRequested;
        _overlay.CloseRequested += Overlay_OnCloseRequested;
        _editor.TextChanged += Editor_OnTextChanged;
        _window.KeyDown += Window_OnKeyDown;
        _window.Closed += Window_OnClosed;
    }

    public static void Attach(Window window)
    {
        _ = new EditorSearchController(window);
    }

    private void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        _overlay.QueryChanged -= Overlay_OnQueryChanged;
        _overlay.NextRequested -= Overlay_OnNextRequested;
        _overlay.PreviousRequested -= Overlay_OnPreviousRequested;
        _overlay.ReplaceNextRequested -= Overlay_OnReplaceNextRequested;
        _overlay.ReplaceAllRequested -= Overlay_OnReplaceAllRequested;
        _overlay.GoToLineRequested -= Overlay_OnGoToLineRequested;
        _overlay.CloseRequested -= Overlay_OnCloseRequested;
        _editor.TextChanged -= Editor_OnTextChanged;
        _window.KeyDown -= Window_OnKeyDown;
        _window.Closed -= Window_OnClosed;
        _host.Children.Remove(_overlay);
    }

    private void Window_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        var commandModifier = eventArgs.KeyModifiers.HasFlag(KeyModifiers.Control)
            || eventArgs.KeyModifiers.HasFlag(KeyModifiers.Meta);

        if (commandModifier && eventArgs.Key == Key.F
            && !eventArgs.KeyModifiers.HasFlag(KeyModifiers.Alt)
            && !eventArgs.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            ShowFind();
            eventArgs.Handled = true;
            return;
        }

        if (commandModifier
            && (eventArgs.Key == Key.H
                || (eventArgs.Key == Key.F && eventArgs.KeyModifiers.HasFlag(KeyModifiers.Alt))))
        {
            ShowReplace();
            eventArgs.Handled = true;
            return;
        }

        if (commandModifier && eventArgs.Key == Key.G)
        {
            ShowGoToLine();
            eventArgs.Handled = true;
            return;
        }

        if (!_overlay.IsVisible)
        {
            return;
        }

        if (eventArgs.Key == Key.F3 && _overlay.Mode is EditorNavigationMode.Find or EditorNavigationMode.Replace)
        {
            SelectRelativeMatch(eventArgs.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1);
            eventArgs.Handled = true;
        }
        else if (eventArgs.Key == Key.Escape)
        {
            CloseOverlay();
            eventArgs.Handled = true;
        }
    }

    private void ShowFind()
    {
        if (!_editor.IsVisible)
        {
            return;
        }

        var initialQuery = GetInitialQuery();
        _overlay.ShowFind(initialQuery);
        RefreshMatches(selectCurrent: initialQuery.Length > 0);
    }

    private void ShowReplace()
    {
        if (!_editor.IsVisible)
        {
            return;
        }

        var initialQuery = GetInitialQuery();
        _overlay.ShowReplace(initialQuery);
        RefreshMatches(selectCurrent: initialQuery.Length > 0);
    }

    private string GetInitialQuery()
    {
        var selectedText = _editor.SelectedText;
        if (selectedText.Length is > 0 and <= MaxSelectionQueryLength
            && !selectedText.Contains('\n', StringComparison.Ordinal)
            && !selectedText.Contains('\r', StringComparison.Ordinal))
        {
            return selectedText;
        }

        return _overlay.Mode is EditorNavigationMode.Find or EditorNavigationMode.Replace
            ? _overlay.Query
            : string.Empty;
    }

    private void ShowGoToLine()
    {
        if (!_editor.IsVisible)
        {
            return;
        }

        var currentLine = _editor.Document.GetLocation(_editor.CaretOffset).Line;
        _overlay.ShowGoToLine(currentLine);
    }

    private void Overlay_OnQueryChanged(object? sender, EventArgs eventArgs)
    {
        RefreshMatches(selectCurrent: true);
    }

    private void Overlay_OnNextRequested(object? sender, EventArgs eventArgs)
    {
        SelectRelativeMatch(1);
    }

    private void Overlay_OnPreviousRequested(object? sender, EventArgs eventArgs)
    {
        SelectRelativeMatch(-1);
    }

    private void Overlay_OnReplaceNextRequested(object? sender, EventArgs eventArgs)
    {
        ReplaceCurrentMatch();
    }

    private void Overlay_OnReplaceAllRequested(object? sender, EventArgs eventArgs)
    {
        ReplaceAllMatches();
    }

    private void Overlay_OnGoToLineRequested(object? sender, EventArgs eventArgs)
    {
        if (!int.TryParse(
                _overlay.Query,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var requestedLine))
        {
            return;
        }

        var line = Math.Clamp(requestedLine, 1, _editor.Document.LineCount);
        var documentLine = _editor.Document.GetLineByNumber(line);
        _editor.CaretOffset = documentLine.Offset;
        _editor.ScrollToLine(line);
        CloseOverlay();
    }

    private void Overlay_OnCloseRequested(object? sender, EventArgs eventArgs)
    {
        CloseOverlay();
    }

    private void Editor_OnTextChanged(object? sender, EventArgs eventArgs)
    {
        if (_overlay.IsVisible && _overlay.Mode is EditorNavigationMode.Find or EditorNavigationMode.Replace)
        {
            RefreshMatches(selectCurrent: false);
        }
    }

    private void RefreshMatches(bool selectCurrent)
    {
        _matches = TextSearchService.FindAll(_editor.Text, _overlay.Query, _overlay.MatchCase);
        if (_matches.Count == 0)
        {
            _selectedMatchIndex = -1;
            _overlay.SetMatchStatus(0, 0);
            return;
        }

        _selectedMatchIndex = FindNearestMatchIndex(_editor.CaretOffset);
        _overlay.SetMatchStatus(_selectedMatchIndex + 1, _matches.Count);
        if (selectCurrent)
        {
            SelectCurrentMatch();
        }
    }

    private int FindNearestMatchIndex(int caretOffset)
    {
        for (var index = 0; index < _matches.Count; index++)
        {
            if (_matches[index].Offset >= caretOffset)
            {
                return index;
            }
        }

        return 0;
    }

    private void SelectRelativeMatch(int direction)
    {
        if (_matches.Count == 0)
        {
            RefreshMatches(selectCurrent: false);
        }

        if (_matches.Count == 0)
        {
            return;
        }

        _selectedMatchIndex = _selectedMatchIndex < 0
            ? 0
            : (_selectedMatchIndex + direction + _matches.Count) % _matches.Count;
        _overlay.SetMatchStatus(_selectedMatchIndex + 1, _matches.Count);
        SelectCurrentMatch();
    }

    private void SelectCurrentMatch()
    {
        if (_selectedMatchIndex < 0 || _selectedMatchIndex >= _matches.Count)
        {
            return;
        }

        var match = _matches[_selectedMatchIndex];
        _editor.Select(match.Offset, match.Length);
        var location = _editor.Document.GetLocation(match.Offset);
        _editor.ScrollTo(location.Line, location.Column);
    }

    private void ReplaceCurrentMatch()
    {
        if (_overlay.Query.Length == 0)
        {
            return;
        }

        if (_matches.Count == 0)
        {
            RefreshMatches(selectCurrent: false);
        }

        if (_matches.Count == 0)
        {
            return;
        }

        if (_selectedMatchIndex < 0 || _selectedMatchIndex >= _matches.Count)
        {
            _selectedMatchIndex = FindNearestMatchIndex(_editor.CaretOffset);
        }

        var match = _matches[_selectedMatchIndex];
        _editor.Document.Replace(match.Offset, match.Length, _overlay.Replacement);
        _editor.CaretOffset = Math.Min(
            match.Offset + _overlay.Replacement.Length,
            _editor.Document.TextLength);
        RefreshMatches(selectCurrent: false);
        if (_matches.Count > 0)
        {
            _selectedMatchIndex = FindNearestMatchIndex(_editor.CaretOffset);
            _overlay.SetMatchStatus(_selectedMatchIndex + 1, _matches.Count);
            SelectCurrentMatch();
        }
    }

    private void ReplaceAllMatches()
    {
        if (_overlay.Query.Length == 0)
        {
            return;
        }

        RefreshMatches(selectCurrent: false);
        if (_matches.Count == 0)
        {
            return;
        }

        _editor.Document.BeginUpdate();
        try
        {
            for (var index = _matches.Count - 1; index >= 0; index--)
            {
                var match = _matches[index];
                _editor.Document.Replace(match.Offset, match.Length, _overlay.Replacement);
            }
        }
        finally
        {
            _editor.Document.EndUpdate();
        }

        RefreshMatches(selectCurrent: false);
        _editor.Focus();
    }

    private void CloseOverlay()
    {
        _overlay.HideOverlay();
        _editor.Focus();
    }

    private void Window_OnClosed(object? sender, EventArgs eventArgs)
    {
        Detach();
    }
}
