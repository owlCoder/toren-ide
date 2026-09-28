using Microsoft.CodeAnalysis;
using Toren.Language.CSharp.Contracts;
using Toren.Language.CSharp.Models;

namespace Toren.Language.CSharp.Services;

public sealed class RoslynCSharpSemanticService : ICSharpSemanticService
{
    private const string SingleDocumentPath = "__toren_active__.cs";

    public Task<CSharpSymbolInfo?> GetSymbolAsync(
        string sourceText,
        int line,
        int column,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceText);

        var context = new CSharpSemanticContext(
            SingleDocumentPath,
            [new CSharpSourceDocument(SingleDocumentPath, sourceText)]);
        return GetSymbolAsync(context, line, column, cancellationToken);
    }

    public Task<CSharpSymbolInfo?> GetSymbolAsync(
        CSharpSemanticContext context,
        int line,
        int column,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Task.Run(
            () => ResolveSymbol(context, line, column, cancellationToken),
            cancellationToken);
    }

    private static CSharpSymbolInfo? ResolveSymbol(
        CSharpSemanticContext context,
        int line,
        int column,
        CancellationToken cancellationToken)
    {
        if (line < 1 || column < 1)
        {
            return null;
        }

        var roslynContext = RoslynCompilationContextFactory.Create(context, cancellationToken);
        if (roslynContext is null || line > roslynContext.SourceText.Lines.Count)
        {
            return null;
        }

        var sourceLine = roslynContext.SourceText.Lines[line - 1];
        var columnOffset = column - 1;
        if (columnOffset > sourceLine.Span.Length)
        {
            return null;
        }

        var position = sourceLine.Start + columnOffset;
        if (position == roslynContext.SourceText.Length && position > 0)
        {
            position--;
        }

        var root = roslynContext.ActiveTree.GetRoot(cancellationToken);
        var token = root.FindToken(position);
        var symbol = FindSymbol(roslynContext.SemanticModel, token.Parent, cancellationToken);
        if (symbol is null)
        {
            return null;
        }

        return new CSharpSymbolInfo(
            symbol.Name,
            symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            RoslynSymbolMapper.MapKind(symbol.Kind),
            FindDefinition(symbol));
    }

    private static ISymbol? FindSymbol(
        SemanticModel semanticModel,
        SyntaxNode? node,
        CancellationToken cancellationToken)
    {
        while (node is not null)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(node, cancellationToken);
            var symbol = symbolInfo.Symbol
                ?? symbolInfo.CandidateSymbols.FirstOrDefault()
                ?? semanticModel.GetDeclaredSymbol(node, cancellationToken);
            if (symbol is not null)
            {
                return symbol;
            }

            node = node.Parent;
        }

        return null;
    }

    private static CSharpSourceLocation? FindDefinition(ISymbol symbol)
    {
        var location = symbol.Locations.FirstOrDefault(candidate => candidate.IsInSource);
        if (location is null)
        {
            return null;
        }

        var linePosition = location.GetLineSpan().StartLinePosition;
        return new CSharpSourceLocation(
            location.SourceTree?.FilePath,
            linePosition.Line + 1,
            linePosition.Character + 1);
    }
}
