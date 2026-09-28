using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Toren.Language.CSharp.Contracts;
using Toren.Language.CSharp.Models;

namespace Toren.Language.CSharp.Services;

public sealed class RoslynCSharpSymbolIndexService : ICSharpSymbolIndexService
{
    public Task<IReadOnlyList<CSharpWorkspaceSymbol>> GetSymbolsAsync(
        CSharpSemanticContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Documents);

        return Task.Run<IReadOnlyList<CSharpWorkspaceSymbol>>(
            () => IndexSymbols(context.Documents, cancellationToken),
            cancellationToken);
    }

    private static CSharpWorkspaceSymbol[] IndexSymbols(
        IReadOnlyList<CSharpSourceDocument> documents,
        CancellationToken cancellationToken)
    {
        var symbols = new List<CSharpWorkspaceSymbol>();

        foreach (var document in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var syntaxTree = CSharpSyntaxTree.ParseText(
                document.Text,
                path: document.Path,
                cancellationToken: cancellationToken);
            var root = syntaxTree.GetRoot(cancellationToken);

            foreach (var node in root.DescendantNodes())
            {
                cancellationToken.ThrowIfCancellationRequested();
                switch (node)
                {
                    case BaseTypeDeclarationSyntax typeDeclaration:
                        symbols.Add(CreateSymbol(
                            typeDeclaration.Identifier,
                            typeDeclaration.Identifier.ValueText,
                            CSharpSymbolKind.Type,
                            GetContainerName(typeDeclaration)));
                        break;
                    case DelegateDeclarationSyntax delegateDeclaration:
                        symbols.Add(CreateSymbol(
                            delegateDeclaration.Identifier,
                            $"{delegateDeclaration.Identifier.ValueText}{delegateDeclaration.ParameterList}",
                            CSharpSymbolKind.Type,
                            GetContainerName(delegateDeclaration)));
                        break;
                    case MethodDeclarationSyntax methodDeclaration:
                        symbols.Add(CreateSymbol(
                            methodDeclaration.Identifier,
                            $"{methodDeclaration.Identifier.ValueText}{methodDeclaration.ParameterList}",
                            CSharpSymbolKind.Method,
                            GetContainerName(methodDeclaration)));
                        break;
                    case ConstructorDeclarationSyntax constructorDeclaration:
                        symbols.Add(CreateSymbol(
                            constructorDeclaration.Identifier,
                            $"{constructorDeclaration.Identifier.ValueText}{constructorDeclaration.ParameterList}",
                            CSharpSymbolKind.Method,
                            GetContainerName(constructorDeclaration)));
                        break;
                    case PropertyDeclarationSyntax propertyDeclaration:
                        symbols.Add(CreateSymbol(
                            propertyDeclaration.Identifier,
                            propertyDeclaration.Identifier.ValueText,
                            CSharpSymbolKind.Property,
                            GetContainerName(propertyDeclaration)));
                        break;
                    case EventDeclarationSyntax eventDeclaration:
                        symbols.Add(CreateSymbol(
                            eventDeclaration.Identifier,
                            eventDeclaration.Identifier.ValueText,
                            CSharpSymbolKind.Event,
                            GetContainerName(eventDeclaration)));
                        break;
                    case EnumMemberDeclarationSyntax enumMember:
                        symbols.Add(CreateSymbol(
                            enumMember.Identifier,
                            enumMember.Identifier.ValueText,
                            CSharpSymbolKind.Field,
                            GetContainerName(enumMember)));
                        break;
                    case VariableDeclaratorSyntax variableDeclaration:
                        AddFieldOrEvent(variableDeclaration, symbols);
                        break;
                }
            }
        }

        return symbols
            .OrderBy(symbol => symbol.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(symbol => symbol.ContainerName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(symbol => symbol.Location.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(symbol => symbol.Location.Line)
            .ToArray();
    }

    private static void AddFieldOrEvent(
        VariableDeclaratorSyntax variableDeclaration,
        List<CSharpWorkspaceSymbol> symbols)
    {
        var declaration = variableDeclaration.Parent?.Parent;
        var kind = declaration switch
        {
            FieldDeclarationSyntax => CSharpSymbolKind.Field,
            EventFieldDeclarationSyntax => CSharpSymbolKind.Event,
            _ => (CSharpSymbolKind?)null,
        };
        if (kind is null)
        {
            return;
        }

        symbols.Add(CreateSymbol(
            variableDeclaration.Identifier,
            variableDeclaration.Identifier.ValueText,
            kind.Value,
            GetContainerName(variableDeclaration)));
    }

    private static CSharpWorkspaceSymbol CreateSymbol(
        SyntaxToken identifier,
        string displayText,
        CSharpSymbolKind kind,
        string? containerName)
    {
        var lineSpan = identifier.GetLocation().GetLineSpan();
        return new CSharpWorkspaceSymbol(
            identifier.ValueText,
            displayText,
            kind,
            containerName,
            new CSharpSourceLocation(
                lineSpan.Path,
                lineSpan.StartLinePosition.Line + 1,
                lineSpan.StartLinePosition.Character + 1));
    }

    private static string? GetContainerName(SyntaxNode node)
    {
        var containingType = node.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault();
        if (containingType is not null)
        {
            return containingType.Identifier.ValueText;
        }

        return node.Ancestors()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .Select(namespaceDeclaration => namespaceDeclaration.Name.ToString())
            .FirstOrDefault();
    }
}
