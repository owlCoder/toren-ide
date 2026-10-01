using Avalonia.Controls;
using Avalonia.Input;
using AvaloniaEdit;
using Toren.App.Editor.Views;
using Toren.Core.IO;
using Toren.Language.CSharp.Contracts;
using Toren.Language.CSharp.Models;

namespace Toren.App.Editor.Services;

internal sealed class CSharpCodeActionController
{
    private readonly Window _window;
    private readonly TextEditor _editor;
    private readonly Grid _host;
    private readonly CSharpCodeActionOverlay _overlay;
    private readonly ICSharpCodeActionService _codeActionService;
    private readonly Func<CSharpSourceDocument?> _activeDocumentAccessor;
    private readonly Func<Task<CSharpSemanticContext?>> _semanticContextAccessor;
    private readonly Action<string> _setStatus;
    private int _requestGeneration;
    private bool _detached;

    private CSharpCodeActionController(
        Window window,
        ICSharpCodeActionService codeActionService,
        Func<CSharpSourceDocument?> activeDocumentAccessor,
        Func<Task<CSharpSemanticContext?>> semanticContextAccessor,
        Action<string> setStatus)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _codeActionService = codeActionService ?? throw new ArgumentNullException(nameof(codeActionService));
        _activeDocumentAccessor = activeDocumentAccessor
            ?? throw new ArgumentNullException(nameof(activeDocumentAccessor));
        _semanticContextAccessor = semanticContextAccessor
            ?? throw new ArgumentNullException(nameof(semanticContextAccessor));
        _setStatus = setStatus ?? throw new ArgumentNullException(nameof(setStatus));
        _editor = window.FindControl<TextEditor>("DocumentEditor")
            ?? throw new InvalidOperationException("The document editor could not be located.");
        _host = window.Content as Grid
            ?? throw new InvalidOperationException("The main window content must be hosted by a Grid.");
        _overlay = new CSharpCodeActionOverlay();
        Grid.SetRow(_overlay, 2);
        Grid.SetColumn(_overlay, 2);
        _host.Children.Add(_overlay);

        _overlay.ActionRequested += Overlay_OnActionRequested;
        _overlay.CloseRequested += Overlay_OnCloseRequested;
        _window.KeyDown += Window_OnKeyDown;
        _window.Closed += Window_OnClosed;
    }

    public static void Attach(
        Window window,
        ICSharpCodeActionService codeActionService,
        Func<CSharpSourceDocument?> activeDocumentAccessor,
        Func<Task<CSharpSemanticContext?>> semanticContextAccessor,
        Action<string> setStatus)
    {
        _ = new CSharpCodeActionController(
            window,
            codeActionService,
            activeDocumentAccessor,
            semanticContextAccessor,
            setStatus);
    }

    private async void Window_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        var commandModifier = eventArgs.KeyModifiers.HasFlag(KeyModifiers.Control)
            || eventArgs.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (!commandModifier
            || eventArgs.Key is not Key.OemPeriod and not Key.Decimal
            || !_editor.IsVisible)
        {
            return;
        }

        eventArgs.Handled = true;
        await ShowActionsAsync().ConfigureAwait(true);
    }

    private async Task ShowActionsAsync()
    {
        var activeDocument = _activeDocumentAccessor();
        if (activeDocument is null)
        {
            return;
        }

        var generation = ++_requestGeneration;
        var sourcePath = activeDocument.Path;
        var sourceText = activeDocument.Text;
        var position = Math.Clamp(_editor.CaretOffset, 0, sourceText.Length);
        var context = await _semanticContextAccessor().ConfigureAwait(true);
        if (!IsRequestCurrent(generation, sourcePath, sourceText) || context is null)
        {
            return;
        }

        var actions = await _codeActionService
            .GetActionsAsync(context, position)
            .ConfigureAwait(true);
        if (!IsRequestCurrent(generation, sourcePath, sourceText))
        {
            return;
        }

        if (actions.Count == 0)
        {
            CloseOverlay();
            _setStatus("No C# quick fixes are available on this line.");
            return;
        }

        _overlay.ShowOverlay(actions);
    }

    private bool IsRequestCurrent(int generation, string sourcePath, string sourceText)
    {
        if (generation != _requestGeneration)
        {
            return false;
        }

        var current = _activeDocumentAccessor();
        return current is not null
            && Path.GetFullPath(current.Path).Equals(
                Path.GetFullPath(sourcePath),
                FileSystemPath.Comparison)
            && current.Text.Equals(sourceText, StringComparison.Ordinal);
    }

    private void Overlay_OnActionRequested(object? sender, EventArgs eventArgs)
    {
        if (_overlay.SelectedAction is not { } action)
        {
            return;
        }

        var activeDocument = _activeDocumentAccessor();
        if (activeDocument is null
            || !PathsEqual(activeDocument.Path, action.Edit.FilePath)
            || action.Edit.StartOffset < 0
            || action.Edit.Length < 0
            || action.Edit.StartOffset > _editor.Document.TextLength
            || action.Edit.Length > _editor.Document.TextLength - action.Edit.StartOffset)
        {
            CloseOverlay();
            _setStatus("The quick fix is stale. Request code actions again.");
            return;
        }

        _editor.Document.Replace(
            action.Edit.StartOffset,
            action.Edit.Length,
            action.Edit.NewText);
        _editor.CaretOffset = Math.Min(
            action.Edit.StartOffset + action.Edit.NewText.Length,
            _editor.Document.TextLength);
        CloseOverlay();
        _editor.Focus();
        _setStatus(action.Title);
    }

    private static bool PathsEqual(string left, string right) =>
        Path.GetFullPath(left).Equals(
            Path.GetFullPath(right),
            FileSystemPath.Comparison);

    private void Overlay_OnCloseRequested(object? sender, EventArgs eventArgs)
    {
        CloseOverlay();
        _editor.Focus();
    }

    private void CloseOverlay()
    {
        _requestGeneration++;
        _overlay.HideOverlay();
    }

    private void Window_OnClosed(object? sender, EventArgs eventArgs)
    {
        Detach();
    }

    private void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        _requestGeneration++;
        _overlay.ActionRequested -= Overlay_OnActionRequested;
        _overlay.CloseRequested -= Overlay_OnCloseRequested;
        _window.KeyDown -= Window_OnKeyDown;
        _window.Closed -= Window_OnClosed;
        _host.Children.Remove(_overlay);
    }
}
