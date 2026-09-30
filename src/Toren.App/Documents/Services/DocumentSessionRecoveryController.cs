using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia.Controls;
using Toren.App.Documents.Contracts;
using Toren.App.ViewModels;

namespace Toren.App.Documents.Services;

internal sealed class DocumentSessionRecoveryController
{
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(700);

    private readonly Window _window;
    private readonly DocumentHostViewModel _documents;
    private readonly IDocumentSessionStore _sessionStore;
    private readonly Action<string> _setStatus;
    private CancellationTokenSource? _saveCancellation;
    private bool _detached;

    private DocumentSessionRecoveryController(
        Window window,
        DocumentHostViewModel documents,
        IDocumentSessionStore sessionStore,
        Action<string> setStatus)
    {
        _window = window;
        _documents = documents;
        _sessionStore = sessionStore;
        _setStatus = setStatus;

        _documents.OpenDocuments.CollectionChanged += OpenDocuments_OnCollectionChanged;
        _documents.PropertyChanged += Documents_OnPropertyChanged;
        foreach (var document in _documents.OpenDocuments)
        {
            document.PropertyChanged += Document_OnPropertyChanged;
        }

        _window.Closed += Window_OnClosed;
    }

    public static void Attach(
        Window window,
        DocumentHostViewModel documents,
        IDocumentSessionStore sessionStore,
        Action<string> setStatus)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(sessionStore);
        ArgumentNullException.ThrowIfNull(setStatus);

        _ = new DocumentSessionRecoveryController(window, documents, sessionStore, setStatus);
    }

    private void OpenDocuments_OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs)
    {
        if (eventArgs.OldItems is not null)
        {
            foreach (OpenDocumentViewModel document in eventArgs.OldItems)
            {
                document.PropertyChanged -= Document_OnPropertyChanged;
            }
        }

        if (eventArgs.NewItems is not null)
        {
            foreach (OpenDocumentViewModel document in eventArgs.NewItems)
            {
                document.PropertyChanged += Document_OnPropertyChanged;
            }
        }

        ScheduleSave();
    }

    private void Documents_OnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(DocumentHostViewModel.ActiveDocument))
        {
            ScheduleSave();
        }
    }

    private void Document_OnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is nameof(OpenDocumentViewModel.Text) or nameof(OpenDocumentViewModel.IsDirty))
        {
            ScheduleSave();
        }
    }

    private void ScheduleSave()
    {
        var previous = Interlocked.Exchange(ref _saveCancellation, null);
        previous?.Cancel();

        var cancellation = new CancellationTokenSource();
        _saveCancellation = cancellation;
        _ = SaveAfterDelayAsync(cancellation);
    }

    private async Task SaveAfterDelayAsync(CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(SaveDelay, cancellation.Token).ConfigureAwait(true);
            var saved = await _sessionStore
                .SaveAsync(_documents.CaptureSession(), cancellation.Token)
                .ConfigureAwait(true);
            if (saved.IsFailure && ReferenceEquals(_saveCancellation, cancellation))
            {
                _setStatus(saved.Error.Message);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            if (ReferenceEquals(_saveCancellation, cancellation))
            {
                _saveCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private void Window_OnClosed(object? sender, EventArgs eventArgs) => Detach();

    private void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        var cancellation = Interlocked.Exchange(ref _saveCancellation, null);
        cancellation?.Cancel();
        _documents.OpenDocuments.CollectionChanged -= OpenDocuments_OnCollectionChanged;
        _documents.PropertyChanged -= Documents_OnPropertyChanged;
        foreach (var document in _documents.OpenDocuments)
        {
            document.PropertyChanged -= Document_OnPropertyChanged;
        }

        _window.Closed -= Window_OnClosed;
    }
}
