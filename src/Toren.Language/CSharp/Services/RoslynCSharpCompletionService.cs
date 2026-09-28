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
        var prefix = GetIdentifierPrefix(sourceLine.ToString(), columnOffset);
        var root = roslynContext.ActiveTree.GetRoot(cancellationToken);
        var lookupContext = FindLookupContext(
            roslynContext.SemanticModel,
            root,
            position,
            cancellationToken);
        IEnumerable<ISymbol> symbols = lookupContext is null
            ? roslynContext.SemanticModel.LookupSymbols(position)
            : roslynContext.SemanticModel.LookupSymbols(
                position,
                lookupContext.Container,
                includeReducedExtensionMethods: true);
        if (lookupContext?.StaticOnly is bool staticOnly)
        {
            symbols = symbols.Where(symbol => IsValidMemberCandidate(symbol, staticOnly));
        }

        var items = symbols
            .Where(symbol => symbol.CanBeReferencedByName && !symbol.IsImplicitlyDeclared)
            .Where(symbol => !string.IsNullOrWhiteSpace(symbol.Name))
            .GroupBy(symbol => (symbol.Name, symbol.Kind))
            .Select(CreateItem)
            .ToList();

        if (lookupContext is null)
        {
            items.AddRange(Keywords.Select(keyword => new CSharpCompletionItem(
                keyword,
                keyword,
                CSharpSymbolKind.Other,
                "C# keyword")));
        }

        return items
            .OrderBy(item => GetMatchOrder(item.DisplayText, prefix))
            .ThenBy(item => GetKindOrder(item.Kind))
            .ThenBy(item => item.DisplayText, StringComparer.OrdinalIgnoreCase)
            .Take(500)
            .ToArray();
    }

    private static MemberLookupContext? FindLookupContext(
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
        switch (expressionSymbol)
        {
            case INamespaceSymbol namespaceSymbol:
                return new MemberLookupContext(namespaceSymbol, null);
            case INamedTypeSymbol namedType:
                return new MemberLookupContext(namedType, true);
            case IAliasSymbol { Target: INamespaceSymbol aliasNamespace }:
                return new MemberLookupContext(aliasNamespace, null);
            case IAliasSymbol { Target: INamedTypeSymbol aliasType }:
                return new MemberLookupContext(aliasType, true);
        }

        return semanticModel.GetTypeInfo(memberAccess.Expression, cancellationToken).Type
            is INamedTypeSymbol instanceType
                ? new MemberLookupContext(instanceType, false)
                : null;
    }

    private static bool IsValidMemberCandidate(ISymbol symbol, bool staticOnly)
    {
        if (staticOnly)
        {
            return symbol.IsStatic || symbol is INamedTypeSymbol;
        }

        return (!symbol.IsStatic && symbol is not INamedTypeSymbol)
            || symbol is IMethodSymbol { MethodKind: MethodKind.ReducedExtension };
    }

    private static CSharpCompletionItem CreateItem(IGrouping<(string Name, SymbolKind Kind), ISymbol> group)
    {
        var symbol = group.First();
        var detail = symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        if (symbol.Kind == SymbolKind.Method)
        {
            var overloadCount = group.Count();
            if (overloadCount > 1)
            {
                detail = $"{detail} (+{overloadCount - 1} overload{(overloadCount == 2 ? string.Empty : "s")})";
            }
        }

        return new CSharpCompletionItem(
            symbol.Name,
            symbol.Name,
            RoslynSymbolMapper.MapKind(symbol.Kind),
            detail.Equals(symbol.Name, StringComparison.Ordinal) ? null : detail);
    }

    private static string GetIdentifierPrefix(string lineText, int columnOffset)
    {
        var end = Math.Clamp(columnOffset, 0, lineText.Length);
        var start = end;
        while (start > 0)
        {
            var character = lineText[start - 1];
            if (!char.IsLetterOrDigit(character) && character != '_')
            {
                break;
            }

            start--;
        }

        return lineText[start..end];
    }

    private static int GetMatchOrder(string candidate, string prefix)
    {
        if (prefix.Length == 0)
        {
            return 0;
        }

        if (candidate.Equals(prefix, StringComparison.Ordinal))
        {
            return 0;
        }

        if (candidate.StartsWith(prefix, StringComparison.Ordinal))
        {
            return 1;
        }

        if (candidate.Equals(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        return candidate.Contains(prefix, StringComparison.OrdinalIgnoreCase) ? 4 : 5;
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

    private sealed record MemberLookupContext(
        INamespaceOrTypeSymbol Container,
        bool? StaticOnly);
}
