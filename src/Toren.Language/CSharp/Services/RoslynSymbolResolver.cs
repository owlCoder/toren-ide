using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Toren.Language.CSharp.Services;

internal static class RoslynSymbolResolver
{
    public static int? GetPosition(SourceText sourceText, int line, int column)
    {
        ArgumentNullException.ThrowIfNull(sourceText);
        if (line < 1 || column < 1 || line > sourceText.Lines.Count)
        {
            return null;
        }

        var sourceLine = sourceText.Lines[line - 1];
        var columnOffset = column - 1;
        if (columnOffset > sourceLine.Span.Length)
        {
            return null;
        }

        var position = sourceLine.Start + columnOffset;
        return position == sourceText.Length && position > 0
            ? position - 1
            : position;
    }

    public static ISymbol? FindSymbol(
        SemanticModel semanticModel,
        SyntaxNode? node,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(semanticModel);

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
}
