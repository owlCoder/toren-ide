using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Toren.Language.CSharp.Contracts;
using Toren.Language.CSharp.Models;

namespace Toren.Language.CSharp.Services;

public sealed class RoslynCSharpCompletionService : ICSharpCompletionService
{
    private static readonly string[] Keywords =
    [
        "abstract", "as", "async", "await", "base", "bool", "break", "byte", "case",
        "catch", "char", "class", "const", "continue", "decimal", "default", "delegate",
        "do", "double", "else", "enum", "event", "explicit", "extern", "false", "finally",
        "fixed", "float", "for", "foreach", "get", "global", "goto", "if", "implicit",
        "in", "init", "int", "interface", "internal", "is", "lock", "long", "namespace",
        "new", "null", "object", "operator", "out", "override", "params", "partial",
        "private", "protected", "public", "readonly", "record", "ref", "required", "return",
        "sbyte", "sealed", "set", "short", "sizeof", "stackalloc", "static", "string",
        "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong",
        "unchecked", "unsafe", "ushort", "using", "var", "virtual", "void", "volatile",
        "while", "with", "yield",
    ];

    public Task<IReadOnlyList<CSharpCompletionItem>> GetCompletionsAsync(
        CSharpSemanticContext context,
        int line,
        int column,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Task.Run<IReadOnlyList<CSharpCompletionItem>>(
            () => GetCompletions(context, line, column, cancellationToken),
            cancellationToken);
    }

    private static CSharpCompletionItem[] GetCompletions(
        CSharpSemanticContext context,
        int line,
        int column,
        CancellationToken cancellationToken)
    {
        if (line < 1 || column < 1)
        {
            return [];
        }

        var roslynContext = RoslynCompilationContextFactory.Create(context, cancellationToken);
        if (roslynContext is null || line > roslynContext.SourceText.Lines.Count)
        {
            return [];
        }

        var sourceLine = roslynContext.SourceText.Lines[line - 1];
        var columnOffset = column - 1;
        if (columnOffset > sourceLine.Span.Length)
        {
            return [];
        }

        var position = sourceLine.Start + columnOffset;
        var root = roslynContext.ActiveTree.GetRoot(cancellationToken);
        var lookupContainer = FindLookupContainer(
            roslynContext.SemanticModel,
            root,
            position,
            cancellationToken);
        var symbols = lookupContainer is null
            ? roslynContext.SemanticModel.LookupSymbols(position)
            : roslynContext.SemanticModel.LookupSymbols(
                position,
                lookupContainer,
                includeReducedExtensionMethods: true);

        var items = symbols
            .Where(symbol => symbol.CanBeReferencedByName && !symbol.IsImplicitlyDeclared)
            .Where(symbol => !string.IsNullOrWhiteSpace(symbol.Name))
            .GroupBy(symbol => (symbol.Name, symbol.Kind))
            .Select(group => CreateItem(group.First()))
            .ToList();

        if (lookupContainer is null)
        {
            items.AddRange(Keywords.Select(keyword => new CSharpCompletionItem(
                keyword,
                keyword,
                CSharpSymbolKind.Other,
                "C# keyword")));
        }

        return items
            .OrderBy(item => GetKindOrder(item.Kind))
            .ThenBy(item => item.DisplayText, StringComparer.OrdinalIgnoreCase)
            .Take(500)
            .ToArray();
    }

    private static INamespaceOrTypeSymbol? FindLookupContainer(
        SemanticModel semanticModel,
        SyntaxNode root,
        int position,
        CancellationToken cancellationToken)
    {
        if (position <= 0)
        {
            return null;
        }

        var token = root.FindToken(Math.Min(position - 1, root.FullSpan.End));
        var memberAccess = token.Parent?
            .AncestorsAndSelf()
            .OfType<MemberAccessExpressionSyntax>()
            .FirstOrDefault(access => access.OperatorToken.Span.End <= position);
        if (memberAccess is null)
        {
            return null;
        }

        var expressionSymbol = semanticModel.GetSymbolInfo(memberAccess.Expression, cancellationToken).Symbol;
        if (expressionSymbol is INamespaceOrTypeSymbol namespaceOrType)
        {
            return namespaceOrType;
        }

        return semanticModel.GetTypeInfo(memberAccess.Expression, cancellationToken).Type
            as INamespaceOrTypeSymbol;
    }

    private static CSharpCompletionItem CreateItem(ISymbol symbol)
    {
        var detail = symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        return new CSharpCompletionItem(
            symbol.Name,
            symbol.Name,
            RoslynSymbolMapper.MapKind(symbol.Kind),
            detail.Equals(symbol.Name, StringComparison.Ordinal) ? null : detail);
    }

    private static int GetKindOrder(CSharpSymbolKind kind) => kind switch
    {
        CSharpSymbolKind.Local => 0,
        CSharpSymbolKind.Parameter => 1,
        CSharpSymbolKind.Property => 2,
        CSharpSymbolKind.Field => 3,
        CSharpSymbolKind.Method => 4,
        CSharpSymbolKind.Event => 5,
        CSharpSymbolKind.Type => 6,
        CSharpSymbolKind.Namespace => 7,
        _ => 8,
    };
}
