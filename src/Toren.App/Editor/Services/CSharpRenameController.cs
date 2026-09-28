using Avalonia.Controls;
using Avalonia.Input;
using AvaloniaEdit;
using Toren.App.Editor.Views;
using Toren.Language.CSharp.Contracts;
using Toren.Language.CSharp.Models;

namespace Toren.App.Editor.Services;

internal sealed class CSharpRenameController
{
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private readonly Window _window;
    private readonly Grid _host;
    private readonly TextEditor _editor;
    private readonly CSharpRenameOverlay _overlay;
    private readonly ICSharpSemanticService _semanticService;
    private readonly ICSharpRenameService _renameService;
    private readonly CSharpRenameChangeApplier _changeApplier;
    private readonly Func<CSharpSourceDocument?> _activeDocumentAccessor;
    private readonly Func<Task<CSharpSemanticContext?>> _semanticContextAccessor;
    private readonly Action<string> _setStatus;
    private PendingRename? _pendingRename;
    private bool _detached;

    private CSharpRenameController(
        Window window,
        ICSharpSemanticService semanticService,
        ICSharpRenameService renameService,
        CSharpRenameChangeApplier changeApplier,
        Func<CSharpSourceDocument?> activeDocumentAccessor,
        Func<Task<CSharpSemanticContext?>> semanticContextAccessor,
        Action<string> setStatus)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _semanticService = semanticService ?? throw new ArgumentNullException(nameof(semanticService));
        _renameService = renameService ?? throw new ArgumentNullException(nameof(renameService));
        _changeApplier = changeApplier ?? throw new ArgumentNullException(nameof(changeApplier));
        _activeDocumentAccessor = activeDocumentAccessor
            ?? throw new ArgumentNullException(nameof(activeDocumentAccessor));
        _semanticContextAccessor = semanticContextAccessor
            ?? throw new ArgumentNullException(nameof(semanticContextAccessor));
        _setStatus = setStatus ?? throw new ArgumentNullException(nameof(setStatus));
        _host = window.Content as Grid
            ?? throw new InvalidOperationException("The main window content must be hosted by a Grid.");
        _editor = window.FindControl<TextEditor>("DocumentEditor")
            ?? throw new InvalidOperationException("The document editor could not be located.");
        _overlay = new CSharpRenameOverlay();
        Grid.SetRow(_overlay, 2);
        Grid.SetColumn(_overlay, 2);
        _host.Children.Add(_overlay);

        _overlay.RenameRequested += Overlay_OnRenameRequested;
        _overlay.CloseRequested += Overlay_OnCloseRequested;
        _window.KeyDown += Window_OnKeyDown;
        _window.Closed += Window_OnClosed;
    }

    public static void Attach(
        Window window,
        ICSharpSemanticService semanticService,
        ICSharpRenameService renameService,
        CSharpRenameChangeApplier changeApplier,
        Func<CSharpSourceDocument?> activeDocumentAccessor,
        Func<Task<CSharpSemanticContext?>> semanticContextAccessor,
        Action<string> setStatus)
    {
        _ = new CSharpRenameController(
            window,
            semanticService,
            renameService,
            changeApplier,
            activeDocumentAccessor,
            semanticContextAccessor,
            setStatus);
    }

    private async void Window_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key != Key.F2 || !_editor.IsVisible || _overlay.IsVisible)
        {
            return;
        }

        eventArgs.Handled = true;
        await ShowRenameAsync().ConfigureAwait(true);
    }

    private async Task ShowRenameAsync()
    {
        var activeDocument = _activeDocumentAccessor();
        if (activeDocument is null)
        {
            return;
        }

        var context = await _semanticContextAccessor().ConfigureAwait(true);
        if (context is null)
        {
            _setStatus("Rename requires a C# project context.");
            return;
        }

        var location = _editor.Document.GetLocation(_editor.CaretOffset);
        var symbol = await _semanticService
            .GetSymbolAsync(context, location.Line, location.Column)
            .ConfigureAwait(true);
        if (symbol?.Definition is null)
        {
            _setStatus("No source symbol is available to rename at the caret.");
            return;
        }

        _pendingRename = new PendingRename(
            activeDocument.Path,
            context,
            location.Line,
            location.Column);
        _overlay.ShowOverlay(symbol.Name);
    }

    private async void Overlay_OnRenameRequested(object? sender, EventArgs eventArgs)
    {
        if (_pendingRename is not { } pending)
        {
            CloseOverlay();
            return;
        }

        var activeDocument = _activeDocumentAccessor();
        if (activeDocument is null
            || !activeDocument.Path.Equals(pending.DocumentPath, PathComparison))
        {
            _overlay.SetError("The active document changed. Start rename again.");
            return;
        }

        var result = await _renameService
            .RenameAsync(
                pending.Context,
                pending.Line,
                pending.Column,
                _overlay.NewName)
            .ConfigureAwait(true);
        if (result is null)
        {
            _overlay.SetError("Rename could not be applied. Check the new identifier.");
            return;
        }

        var applied = await _changeApplier.ApplyAsync(result).ConfigureAwait(true);
        if (!applied.IsSuccess)
        {
            _overlay.SetError(applied.Error.Message);
            _setStatus(applied.Error.Message);
            return;
        }

        CloseOverlay();
        _setStatus($"Renamed {result.OriginalName} to {result.NewName} in {applied.Value} document(s).");
    }

    private void Overlay_OnCloseRequested(object? sender, EventArgs eventArgs)
    {
        CloseOverlay();
    }

    private void CloseOverlay()
    {
        _pendingRename = null;
        _overlay.HideOverlay();
        _editor.Focus();
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
        _overlay.RenameRequested -= Overlay_OnRenameRequested;
        _overlay.CloseRequested -= Overlay_OnCloseRequested;
        _window.KeyDown -= Window_OnKeyDown;
        _window.Closed -= Window_OnClosed;
        _host.Children.Remove(_overlay);
    }

    private sealed record PendingRename(
        string DocumentPath,
        CSharpSemanticContext Context,
        int Line,
        int Column);
}
