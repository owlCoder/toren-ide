using Toren.App.Documents.Contracts;
using Toren.App.Documents.Models;
using Toren.App.ViewModels;
using Toren.Core.Results;
using Toren.Language.CSharp.Models;

namespace Toren.App.Editor.Services;

internal sealed class CSharpRenameChangeApplier(
    DocumentHostViewModel documents,
    ITextDocumentStore documentStore)
{
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private readonly DocumentHostViewModel _documents = documents
        ?? throw new ArgumentNullException(nameof(documents));
    private readonly ITextDocumentStore _documentStore = documentStore
        ?? throw new ArgumentNullException(nameof(documentStore));

    public async Task<Result<int>> ApplyAsync(
        CSharpRenameResult renameResult,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(renameResult);

        var openDocuments = _documents.OpenDocuments.ToDictionary(
            document => Path.GetFullPath(document.Path),
            PathComparer);
        var openChanges = new List<(OpenDocumentViewModel Document, string Text)>();
        var closedChanges = new List<(TextDocumentContent Original, string NewText)>();

        foreach (var change in renameResult.Documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fullPath = Path.GetFullPath(change.Path);
            if (openDocuments.TryGetValue(fullPath, out var openDocument))
            {
                openChanges.Add((openDocument, change.Text));
                continue;
            }

            var loaded = await _documentStore.LoadAsync(fullPath, cancellationToken).ConfigureAwait(false);
            if (!loaded.IsSuccess)
            {
                return Result.Failure<int>(loaded.Error);
            }

            closedChanges.Add((loaded.Value, change.Text));
        }

        var savedOriginals = new List<TextDocumentContent>(closedChanges.Count);
        try
        {
            foreach (var change in closedChanges)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var updated = change.Original with { Text = change.NewText };
                var saved = await _documentStore.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
                if (!saved.IsSuccess)
                {
                    await RollBackAsync(savedOriginals).ConfigureAwait(false);
                    return Result.Failure<int>(saved.Error);
                }

                savedOriginals.Add(change.Original);
            }
        }
        catch (OperationCanceledException)
        {
            await RollBackAsync(savedOriginals).ConfigureAwait(false);
            throw;
        }

        foreach (var change in openChanges)
        {
            change.Document.Text = change.Text;
        }

        return Result.Success(renameResult.Documents.Count);
    }

    private async Task RollBackAsync(IReadOnlyList<TextDocumentContent> originals)
    {
        foreach (var original in originals.Reverse())
        {
            _ = await _documentStore.SaveAsync(original, CancellationToken.None).ConfigureAwait(false);
        }
    }
}
