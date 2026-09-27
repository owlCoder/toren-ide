using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using AvaloniaEdit;
using Toren.App.Editor.Models;
using Toren.App.Editor.Views;
using Toren.Language.CSharp.Contracts;
using Toren.Language.CSharp.Models;

namespace Toren.App.Editor.Services;

public sealed class EditorNavigationController
{
    private readonly Window _window;
    private readonly TextEditor _editor;
    private readonly Grid _host;
    private readonly EditorNavigationOverlay _overlay;
    private readonly ICSharpSemanticService _semanticService;
    private readonly Stack<int> _navigationHistory = new();
    private IReadOnlyList<TextSearchMatch> _matches = [];
    private int _selectedMatchIndex = -1;
    private bool _detached;

    public EditorNavigationController(Window window, ICSharpSemanticService semanticService)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _semanticService = semanticService ?? throw new ArgumentNullException(nameof(semanticService));
        _editor = window.FindControl<TextEditor>("DocumentEditor")
            ?? throw new InvalidOperationException("The document editor could not be located.");
        _host = _editor.Parent as Grid
            ?? throw new InvalidOperationException("The document editor must be hosted by a Grid.");
        _overlay = new EditorNavigationOverlay();
        _host.Children.Add(_overlay);

        _overlay.QueryChanged += Overlay_OnQueryChanged;
        _overlay.NextRequested += Overlay_OnNextRequested;
        _overlay.PreviousRequested += Overlay_OnPreviousRequested;
        _overlay.GoToLineRequested += Overlay_OnGoToLineRequested;
        _overlay.CloseRequested += Overlay_OnCloseRequested;
        _editor.TextChanged += Editor_OnTextChanged;
        _window.KeyDown += Window_OnKeyDown;
        _window.Closed += Window_OnClosed;
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
        _overlay.GoToLineRequested -= Overlay_OnGoToLineRequested;
        _overlay.CloseRequested -= Overlay_OnCloseRequested;
        _editor.TextChanged -= Editor_OnTextChanged;
        _window.KeyDown -= Window_OnKeyDown;
        _window.Closed -= Window_OnClosed;
        _host.Children.Remove(_overlay);
    }

    private async void Window_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        var commandModifier = eventArgs.KeyModifiers.HasFlag(KeyModifiers.Control)
            || eventArgs.KeyModifiers.HasFlag(KeyModifiers.Meta);

        if (commandModifier && eventArgs.Key == Key.F)
        {
            ShowFind();
            eventArgs.Handled = true;
            return;
        }

        if (commandModifier && eventArgs.Key == Key.G)
        {
            ShowGoToLine();
            eventArgs.Handled = true;
            return;
        }

        if (eventArgs.Key == Key.F12 && _editor.IsVisible)
        {
            eventArgs.Handled = true;
            await GoToDefinitionAsync().ConfigureAwait(true);
            return;
        }

        if (eventArgs.Key == Key.Left
            && eventArgs.KeyModifiers.HasFlag(KeyModifiers.Alt)
            && _navigationHistory.Count > 0)
        {
            NavigateBack();
            eventArgs.Handled = true;
            return;
        }

        if (!_overlay.IsVisible)
        {
            return;
        }

        if (eventArgs.Key == Key.F3)
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

        var selectedText = _editor.SelectedText;
        var initialQuery = selectedText.Length <= 120
            && !selectedText.Contains('\n', StringComparison.Ordinal)
            && !selectedText.Contains('\r', StringComparison.Ordinal)
                ? selectedText
                : _overlay.Mode == EditorNavigationMode.Find ? _overlay.Query : string.Empty;

        _overlay.ShowFind(initialQuery);
        RefreshMatches(selectCurrent: initialQuery.Length > 0);
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

    private async Task GoToDefinitionAsync()
    {
        var originOffset = _editor.CaretOffset;
        var origin = _editor.Document.GetLocation(originOffset);
        var symbol = await _semanticService
            .GetSymbolAsync(_editor.Text, origin.Line, origin.Column)
            .ConfigureAwait(true);
        if (symbol?.Definition is null)
        {
            return;
        }

        _navigationHistory.Push(originOffset);
        NavigateTo(symbol.Definition);
    }

    private void NavigateTo(CSharpSourceLocation location)
    {
        var line = Math.Clamp(location.Line, 1, _editor.Document.LineCount);
        var documentLine = _editor.Document.GetLineByNumber(line);
        var column = Math.Clamp(location.Column, 1, documentLine.Length + 1);
        _editor.CaretOffset = documentLine.Offset + column - 1;
        _editor.ScrollTo(line, column);
        _editor.Focus();
    }

    private void NavigateBack()
    {
        var offset = Math.Clamp(_navigationHistory.Pop(), 0, _editor.Document.TextLength);
        _editor.CaretOffset = offset;
        var location = _editor.Document.GetLocation(offset);
        _editor.ScrollTo(location.Line, location.Column);
        _editor.Focus();
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
        if (_overlay.IsVisible && _overlay.Mode == EditorNavigationMode.Find)
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

        var caretOffset = _editor.CaretOffset;
        _selectedMatchIndex = FindNearestMatchIndex(caretOffset);
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

        if (_selectedMatchIndex < 0)
        {
            _selectedMatchIndex = 0;
        }
        else
        {
            _selectedMatchIndex = (_selectedMatchIndex + direction + _matches.Count) % _matches.Count;
        }

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
