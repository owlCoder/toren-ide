using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Toren.App.Documents.Contracts;
using Toren.Core.Results;

namespace Toren.App.ViewModels;

public sealed partial class DocumentHostViewModel(ITextDocumentStore documentStore) : ObservableObject
{
    private readonly ITextDocumentStore _documentStore = documentStore
        ?? throw new ArgumentNullException(nameof(documentStore));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveDocument))]
    private OpenDocumentViewModel? _activeDocument;

    public ObservableCollection<OpenDocumentViewModel> OpenDocuments { get; } = new();

    public bool HasActiveDocument => ActiveDocument is not null;

    public async Task<Result<OpenDocumentViewModel>> OpenAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = Path.GetFullPath(path);
        var existing = OpenDocuments.FirstOrDefault(document =>
            document.Path.Equals(fullPath, StringComparison.Ordinal));
        if (existing is not null)
        {
            Activate(existing);
            return Result.Success(existing);
        }

        var loaded = await _documentStore.LoadAsync(fullPath, cancellationToken).ConfigureAwait(true);
        if (!loaded.IsSuccess)
        {
            return Result.Failure<OpenDocumentViewModel>(loaded.Error);
        }

        var document = new OpenDocumentViewModel(loaded.Value);
        OpenDocuments.Add(document);
        Activate(document);
        return Result.Success(document);
    }

    public void Activate(OpenDocumentViewModel document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!OpenDocuments.Contains(document))
        {
            throw new ArgumentException("Document is not open in this host.", nameof(document));
        }

        ActiveDocument = document;
    }

    public void Deactivate() => ActiveDocument = null;

    public bool TryClose(OpenDocumentViewModel document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!OpenDocuments.Contains(document) || document.IsDirty)
        {
            return false;
        }

        var index = OpenDocuments.IndexOf(document);
        var wasActive = ReferenceEquals(ActiveDocument, document);
        OpenDocuments.Remove(document);

        if (wasActive)
        {
            ActiveDocument = OpenDocuments.Count == 0
                ? null
                : OpenDocuments[Math.Min(index, OpenDocuments.Count - 1)];
        }

        return true;
    }

    public async Task<Result<OpenDocumentViewModel>> SaveActiveAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ActiveDocument is null)
        {
            return Result.Failure<OpenDocumentViewModel>(
                OperationError.Create("document.save.no-active", "No active document is available to save."));
        }

        var saved = await _documentStore
            .SaveAsync(ActiveDocument.CreateContent(), cancellationToken)
            .ConfigureAwait(true);
        if (!saved.IsSuccess)
        {
            return Result.Failure<OpenDocumentViewModel>(saved.Error);
        }

        ActiveDocument.MarkSaved();
        return Result.Success(ActiveDocument);
    }

    partial void OnActiveDocumentChanged(
        OpenDocumentViewModel? oldValue,
        OpenDocumentViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.IsActive = false;
        }

        if (newValue is not null)
        {
            newValue.IsActive = true;
        }
    }
}
