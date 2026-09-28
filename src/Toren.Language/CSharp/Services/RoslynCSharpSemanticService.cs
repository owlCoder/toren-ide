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
        var roslynContext = RoslynCompilationContextFactory.Create(context, cancellationToken);
        if (roslynContext is null)
        {
            return null;
        }

        var position = RoslynSymbolResolver.GetPosition(roslynContext.SourceText, line, column);
        if (position is null)
        {
            return null;
        }

        var root = roslynContext.ActiveTree.GetRoot(cancellationToken);
        var token = root.FindToken(position.Value);
        var symbol = RoslynSymbolResolver.FindSymbol(
            roslynContext.SemanticModel,
            token.Parent,
            cancellationToken);
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
