using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Toren.Language.CSharp.Contracts;
using Toren.Language.CSharp.Models;

namespace Toren.Language.CSharp.Services;

public sealed class RoslynCSharpSemanticHighlightingService : ICSharpSemanticHighlightingService
{
    public Task<IReadOnlyList<CSharpSemanticHighlight>> GetHighlightsAsync(
        CSharpSemanticContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Task.Run<IReadOnlyList<CSharpSemanticHighlight>>(
            () => GetHighlights(context, cancellationToken),
            cancellationToken);
    }

    private static CSharpSemanticHighlight[] GetHighlights(
        CSharpSemanticContext context,
        CancellationToken cancellationToken)
    {
        var roslynContext = RoslynCompilationContextFactory.Create(context, cancellationToken);
        if (roslynContext is null)
        {
            return [];
        }

        var root = roslynContext.ActiveTree.GetRoot(cancellationToken);
        var highlights = new List<CSharpSemanticHighlight>();
        foreach (var token in root.DescendantTokens())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!token.IsKind(SyntaxKind.IdentifierToken) || token.Span.Length == 0)
            {
                continue;
            }

            var symbol = RoslynSymbolResolver.FindSymbol(
                roslynContext.SemanticModel,
                token.Parent,
                cancellationToken);
            if (symbol is null || symbol.IsImplicitlyDeclared)
            {
                continue;
            }

            var kind = symbol.Kind == SymbolKind.TypeParameter
                ? CSharpSymbolKind.Type
                : RoslynSymbolMapper.MapKind(symbol.Kind);
            if (kind == CSharpSymbolKind.Other)
            {
                continue;
            }

            highlights.Add(new CSharpSemanticHighlight(
                token.SpanStart,
                token.Span.Length,
                kind));
        }

        return highlights
            .OrderBy(static highlight => highlight.StartOffset)
            .ThenBy(static highlight => highlight.Length)
            .ToArray();
    }
}
