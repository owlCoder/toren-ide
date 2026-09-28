using System.ComponentModel;
using Avalonia.Controls;
using AvaloniaEdit;
using Toren.App.Diagnostics.Contracts;
using Toren.App.Diagnostics.ViewModels;
using Toren.App.ViewModels;
using Toren.Language.CSharp.Models;

namespace Toren.App.Diagnostics.Services;

internal sealed class CSharpDiagnosticsController
{
    private readonly Window _window;
    private readonly TextEditor _editor;
    private readonly DocumentHostViewModel _documents;
    private readonly ProblemsViewModel _problems;
    private readonly IDocumentDiagnosticsCoordinator _coordinator;
    private readonly Func<CSharpSourceDocument?> _activeDocumentAccessor;
    private readonly Func<Task<CSharpSemanticContext?>> _semanticContextAccessor;
    private bool _detached;

    private CSharpDiagnosticsController(
        Window window,
        DocumentHostViewModel documents,
        ProblemsViewModel problems,
        IDocumentDiagnosticsCoordinator coordinator,
        Func<CSharpSourceDocument?> activeDocumentAccessor,
        Func<Task<CSharpSemanticContext?>> semanticContextAccessor)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _documents = documents ?? throw new ArgumentNullException(nameof(documents));
        _problems = problems ?? throw new ArgumentNullException(nameof(problems));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _activeDocumentAccessor = activeDocumentAccessor
            ?? throw new ArgumentNullException(nameof(activeDocumentAccessor));
        _semanticContextAccessor = semanticContextAccessor
            ?? throw new ArgumentNullException(nameof(semanticContextAccessor));
        _editor = window.FindControl<TextEditor>("DocumentEditor")
            ?? throw new InvalidOperationException("The document editor could not be located.");

        _editor.TextChanged += Editor_OnTextChanged;
        _documents.PropertyChanged += Documents_OnPropertyChanged;
        _window.Closed += Window_OnClosed;
    }

    public static void Attach(
        Window window,
        DocumentHostViewModel documents,
        ProblemsViewModel problems,
        IDocumentDiagnosticsCoordinator coordinator,
        Func<CSharpSourceDocument?> activeDocumentAccessor,
        Func<Task<CSharpSemanticContext?>> semanticContextAccessor)
    {
        _ = new CSharpDiagnosticsController(
            window,
            documents,
            problems,
            coordinator,
            activeDocumentAccessor,
            semanticContextAccessor);
    }

    private async void Editor_OnTextChanged(object? sender, EventArgs eventArgs)
    {
        await RefreshAsync(debounce: true).ConfigureAwait(true);
    }

    private async void Documents_OnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(DocumentHostViewModel.ActiveDocument))
        {
            await RefreshAsync(debounce: false).ConfigureAwait(true);
        }
    }

    private async Task RefreshAsync(bool debounce)
    {
        var document = _activeDocumentAccessor();
        if (document is null
            || !Path.GetExtension(document.Path).Equals(".cs", StringComparison.OrdinalIgnoreCase))
        {
            _coordinator.CancelPending();
            _problems.Clear();
            return;
        }

        var analyzedPath = document.Path;
        var analyzedText = document.Text;
        var semanticContext = await _semanticContextAccessor().ConfigureAwait(true);
        var diagnostics = await _coordinator
            .AnalyzeLatestAsync(analyzedText, semanticContext, debounce)
            .ConfigureAwait(true);
        if (diagnostics is null)
        {
            return;
        }

        var currentDocument = _activeDocumentAccessor();
        if (currentDocument is null
            || !currentDocument.Path.Equals(analyzedPath, StringComparison.Ordinal)
            || !currentDocument.Text.Equals(analyzedText, StringComparison.Ordinal))
        {
            return;
        }

        _problems.Replace(analyzedPath, diagnostics);
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
        _editor.TextChanged -= Editor_OnTextChanged;
        _documents.PropertyChanged -= Documents_OnPropertyChanged;
        _window.Closed -= Window_OnClosed;
        _coordinator.CancelPending();
    }
}
