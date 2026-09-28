using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Rename;
using Toren.Language.CSharp.Contracts;
using Toren.Language.CSharp.Models;

namespace Toren.Language.CSharp.Services;

public sealed class RoslynCSharpRenameService : ICSharpRenameService
{
    public async Task<CSharpRenameResult?> RenameAsync(
        CSharpSemanticContext context,
        int line,
        int column,
        string newName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);

        if (!SyntaxFacts.IsValidIdentifier(newName))
        {
            return null;
        }

        using var roslynContext = RoslynWorkspaceContextFactory.Create(context, cancellationToken);
        if (roslynContext is null)
        {
            return null;
        }

        var activeDocument = roslynContext.ActiveDocument;
        var sourceText = await activeDocument.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var position = RoslynSymbolResolver.GetPosition(sourceText, line, column);
        if (position is null)
        {
            return null;
        }

        var root = await activeDocument.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var semanticModel = await activeDocument.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return null;
        }

        var token = root.FindToken(position.Value);
        var symbol = RoslynSymbolResolver.FindSymbol(semanticModel, token.Parent, cancellationToken);
        if (symbol is null
            || !symbol.Locations.Any(location => location.IsInSource)
            || symbol.Name.Equals(newName, StringComparison.Ordinal))
        {
            return null;
        }

        var renamedSolution = await Renamer.RenameSymbolAsync(
                roslynContext.Solution,
                symbol,
                new SymbolRenameOptions(),
                newName,
                cancellationToken)
            .ConfigureAwait(false);
        var changedDocuments = await GetChangedDocumentsAsync(
                roslynContext.Solution,
                renamedSolution,
                cancellationToken)
            .ConfigureAwait(false);
        if (changedDocuments.Count == 0)
        {
            return null;
        }

        return new CSharpRenameResult(symbol.Name, newName, changedDocuments);
    }

    private static async Task<IReadOnlyList<CSharpRenamedDocument>> GetChangedDocumentsAsync(
        Solution originalSolution,
        Solution renamedSolution,
        CancellationToken cancellationToken)
    {
        var changes = new List<CSharpRenamedDocument>();
        foreach (var projectChange in renamedSolution.GetChanges(originalSolution).GetProjectChanges())
        {
            foreach (var documentId in projectChange.GetChangedDocuments())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var document = renamedSolution.GetDocument(documentId);
                if (document?.FilePath is not { Length: > 0 } path)
                {
                    continue;
                }

                var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
                changes.Add(new CSharpRenamedDocument(path, text.ToString()));
            }
        }

        return changes
            .OrderBy(change => change.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
