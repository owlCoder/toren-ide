using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Toren.App.Documents.Contracts;
using Toren.App.Documents.Models;
using Toren.App.Layout.Models;
using Toren.Core.Results;

namespace Toren.App.ViewModels;

/// <remarks>
/// One shared tab strip feeds up to two editors. <see cref="PrimaryDocument"/> and
/// <see cref="SecondaryDocument"/> are what the two editors show, and <see cref="ActiveDocument"/>
/// is the document of the editor that has focus (or null while the Welcome tab is selected).
/// </remarks>
public sealed partial class DocumentHostViewModel(ITextDocumentStore documentStore) : ObservableObject
{
    private readonly ITextDocumentStore _documentStore = documentStore
        ?? throw new ArgumentNullException(nameof(documentStore));

    private string? _workspaceRoot;
    private bool _isSecondaryFocused;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveDocument))]
    private OpenDocumentViewModel? _activeDocument;

    [ObservableProperty]
    private OpenDocumentViewModel? _primaryDocument;

    [ObservableProperty]
    private OpenDocumentViewModel? _secondaryDocument;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSplit))]
    private EditorSplitOrientation _splitOrientation;

    public ObservableCollection<OpenDocumentViewModel> OpenDocuments { get; } = new();

    public bool HasActiveDocument => ActiveDocument is not null;

    public bool HasDirtyDocuments => OpenDocuments.Any(document => document.IsDirty);

    public bool IsSplit => SplitOrientation != EditorSplitOrientation.None;

    /// <summary>The folder breadcrumbs are shown relative to.</summary>
    public string? WorkspaceRoot
    {
        get => _workspaceRoot;
        set
        {
            _workspaceRoot = value;
            foreach (var document in OpenDocuments)
            {
                document.UpdateBreadcrumbs(value);
            }
        }
    }

    public async Task<Result<OpenDocumentViewModel>> OpenAsync(
        string path,
        CancellationToken cancellationToken = default,
        bool preview = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = Path.GetFullPath(path);
        var existing = FindOpen(fullPath);
        if (existing is not null)
        {
            if (!preview)
            {
                existing.IsPreview = false;
            }

            Activate(existing);
            return Result.Success(existing);
        }

        var loaded = await _documentStore.LoadAsync(fullPath, cancellationToken).ConfigureAwait(true);
        if (!loaded.IsSuccess)
        {
            return Result.Failure<OpenDocumentViewModel>(loaded.Error);
        }

        var document = new OpenDocumentViewModel(loaded.Value);
        document.UpdateBreadcrumbs(WorkspaceRoot);

        var replaced = preview
            ? OpenDocuments.FirstOrDefault(candidate => candidate.IsPreview && !candidate.IsPinned && !candidate.IsDirty)
            : null;
        if (replaced is not null)
        {
            document.IsPreview = true;
            OpenDocuments[OpenDocuments.IndexOf(replaced)] = document;
            if (ReferenceEquals(PrimaryDocument, replaced))
            {
                PrimaryDocument = document;
            }

            if (ReferenceEquals(SecondaryDocument, replaced))
            {
                SecondaryDocument = document;
            }
        }
        else
        {
            document.IsPreview = preview;
            OpenDocuments.Add(document);
        }

        Activate(document);
        return Result.Success(document);
    }

    public async Task<int> RestoreSessionAsync(
        DocumentSessionState session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(session.OpenDocumentPaths);

        var restoredCount = 0;
        foreach (var path in session.OpenDocumentPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var opened = await OpenAsync(path, cancellationToken).ConfigureAwait(true);
            if (opened.IsSuccess)
            {
                restoredCount++;
            }
        }

        foreach (var snapshot in session.RecoveryDocuments ?? [])
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fullPath = Path.GetFullPath(snapshot.Path);
            var document = FindOpen(fullPath);
            if (document is null)
            {
                document = new OpenDocumentViewModel(
                    new TextDocumentContent(fullPath, snapshot.Text, snapshot.Encoding));
                document.UpdateBreadcrumbs(WorkspaceRoot);
                document.IsDirty = true;
                OpenDocuments.Add(document);
                restoredCount++;
            }
            else
            {
                document.Text = snapshot.Text;
                document.IsDirty = true;
            }
        }

        if (session.ActiveDocumentPath is not null)
        {
            var active = FindOpen(session.ActiveDocumentPath);
            if (active is not null)
            {
                Activate(active);
            }
        }

        return restoredCount;
    }

    public DocumentSessionState CaptureSession() =>
        new(
            OpenDocuments.Select(document => document.Path).ToArray(),
            ActiveDocument?.Path,
            OpenDocuments
                .Where(document => document.IsDirty)
                .Select(document => new DocumentRecoverySnapshot(
                    document.Path,
                    document.Text,
                    document.Encoding))
                .ToArray());

    /// <summary>Shows the document in the focused editor and makes it the active one.</summary>
    public void Activate(OpenDocumentViewModel document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!OpenDocuments.Contains(document))
        {
            throw new ArgumentException("Document is not open in this host.", nameof(document));
        }

        if (_isSecondaryFocused && IsSplit)
        {
            SecondaryDocument = document;
        }
        else
        {
            PrimaryDocument = document;
        }

        ActiveDocument = document;
    }

    public void Deactivate() => ActiveDocument = null;

    /// <summary>Called when the user moves focus between the two editors.</summary>
    public void FocusPane(bool secondary)
    {
        if (secondary && !IsSplit)
        {
            return;
        }

        _isSecondaryFocused = secondary;
        var document = secondary ? SecondaryDocument : PrimaryDocument;
        if (document is not null)
        {
            ActiveDocument = document;
        }
    }

    public void SetSplit(EditorSplitOrientation orientation)
    {
        if (orientation == EditorSplitOrientation.None)
        {
            CloseSplit();
            return;
        }

        var source = PrimaryDocument ?? OpenDocuments.FirstOrDefault();
        if (source is null)
        {
            return;
        }

        PrimaryDocument ??= source;
        SecondaryDocument ??= source;
        SplitOrientation = orientation;
    }

    public void CloseSplit()
    {
        if (!IsSplit)
        {
            return;
        }

        var keep = _isSecondaryFocused ? SecondaryDocument : PrimaryDocument;
        SplitOrientation = EditorSplitOrientation.None;
        SecondaryDocument = null;
        _isSecondaryFocused = false;
        PrimaryDocument = keep ?? PrimaryDocument;
        if (ActiveDocument is not null)
        {
            ActiveDocument = PrimaryDocument;
        }
    }

    public void TogglePin(OpenDocumentViewModel document) => SetPinned(document, !document.IsPinned);

    public void SetPinned(OpenDocumentViewModel document, bool pinned)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!OpenDocuments.Contains(document) || document.IsPinned == pinned)
        {
            return;
        }

        document.IsPinned = pinned;
        if (pinned)
        {
            document.IsPreview = false;
        }

        // Pinned tabs form a block on the left. Both pinning and unpinning put the tab
        // right after the other pinned tabs.
        var pinnedCount = OpenDocuments.Count(candidate => candidate.IsPinned && !ReferenceEquals(candidate, document));
        var from = OpenDocuments.IndexOf(document);
        if (from != pinnedCount)
        {
            OpenDocuments.Move(from, pinnedCount);
        }
    }

    /// <summary>Restores pinned tabs and the split from a stored workspace layout.</summary>
    public void ApplyLayout(WorkspaceLayoutState layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var pinnedPaths = layout.PinnedDocumentPaths ?? [];
        foreach (var document in OpenDocuments.ToArray())
        {
            if (!pinnedPaths.Contains(document.Path, StringComparer.Ordinal))
            {
                SetPinned(document, false);
            }
        }

        foreach (var path in pinnedPaths)
        {
            if (FindOpen(path) is { } pinned)
            {
                SetPinned(pinned, true);
            }
        }

        if (layout.SplitOrientation == EditorSplitOrientation.None)
        {
            CloseSplit();
            return;
        }

        SetSplit(layout.SplitOrientation);
        if (IsSplit
            && layout.SecondaryDocumentPath is { } secondaryPath
            && FindOpen(secondaryPath) is { } secondary)
        {
            SecondaryDocument = secondary;
        }
    }

    public bool TryClose(OpenDocumentViewModel document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!OpenDocuments.Contains(document) || document.IsDirty || document.IsPinned)
        {
            return false;
        }

        var index = OpenDocuments.IndexOf(document);
        var wasActive = ReferenceEquals(ActiveDocument, document);
        OpenDocuments.Remove(document);

        OpenDocumentViewModel? Neighbour() => OpenDocuments.Count == 0
            ? null
            : OpenDocuments[Math.Min(index, OpenDocuments.Count - 1)];

        if (ReferenceEquals(PrimaryDocument, document))
        {
            PrimaryDocument = Neighbour();
        }

        if (ReferenceEquals(SecondaryDocument, document))
        {
            SecondaryDocument = Neighbour();
        }

        if (OpenDocuments.Count == 0)
        {
            SplitOrientation = EditorSplitOrientation.None;
            SecondaryDocument = null;
            _isSecondaryFocused = false;
        }

        if (wasActive)
        {
            ActiveDocument = _isSecondaryFocused && IsSplit ? SecondaryDocument : PrimaryDocument;
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

    private OpenDocumentViewModel? FindOpen(string path) =>
        OpenDocuments.FirstOrDefault(document => document.Path.Equals(path, StringComparison.Ordinal));
}
