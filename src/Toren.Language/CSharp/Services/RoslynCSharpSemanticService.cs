using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Toren.Language.CSharp.Contracts;
using Toren.Language.CSharp.Models;

namespace Toren.Language.CSharp.Services;

public sealed class RoslynCSharpSemanticService : ICSharpSemanticService
{
    private const string SingleDocumentPath = "__toren_active__.cs";

    private static readonly Lazy<MetadataReference[]> MetadataReferences =
        new(CreateMetadataReferences);

    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

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
        ArgumentException.ThrowIfNullOrWhiteSpace(context.ActiveDocumentPath);
        ArgumentNullException.ThrowIfNull(context.Documents);

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
        if (line < 1 || column < 1 || context.Documents.Count == 0)
        {
            return null;
        }

        var syntaxTrees = context.Documents
            .Select(document => CSharpSyntaxTree.ParseText(
                document.Text,
                path: document.Path,
                cancellationToken: cancellationToken))
            .ToArray();
        var activeTree = syntaxTrees.FirstOrDefault(tree =>
            tree.FilePath.Equals(context.ActiveDocumentPath, PathComparison));
        if (activeTree is null)
        {
            return null;
        }

        var source = activeTree.GetText(cancellationToken);
        if (line > source.Lines.Count)
        {
            return null;
        }

        var sourceLine = source.Lines[line - 1];
        var columnOffset = column - 1;
        if (columnOffset > sourceLine.Span.Length)
        {
            return null;
        }

        var position = sourceLine.Start + columnOffset;
        if (position == source.Length && position > 0)
        {
            position--;
        }

        var compilation = CSharpCompilation.Create(
            "Toren.SemanticAnalysis",
            syntaxTrees,
            MetadataReferences.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var semanticModel = compilation.GetSemanticModel(activeTree, ignoreAccessibility: true);
        var root = activeTree.GetRoot(cancellationToken);
        var token = root.FindToken(position);
        var symbol = FindSymbol(semanticModel, token.Parent, cancellationToken);
        if (symbol is null)
        {
            return null;
        }

        return new CSharpSymbolInfo(
            symbol.Name,
            symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            MapKind(symbol.Kind),
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

    private static CSharpSymbolKind MapKind(SymbolKind kind) => kind switch
    {
        SymbolKind.Namespace => CSharpSymbolKind.Namespace,
        SymbolKind.NamedType => CSharpSymbolKind.Type,
        SymbolKind.Method => CSharpSymbolKind.Method,
        SymbolKind.Property => CSharpSymbolKind.Property,
        SymbolKind.Field => CSharpSymbolKind.Field,
        SymbolKind.Event => CSharpSymbolKind.Event,
        SymbolKind.Parameter => CSharpSymbolKind.Parameter,
        SymbolKind.Local => CSharpSymbolKind.Local,
        _ => CSharpSymbolKind.Other,
    };

    private static MetadataReference[] CreateMetadataReferences()
    {
        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        var paths = string.IsNullOrWhiteSpace(trustedPlatformAssemblies)
            ? new[] { typeof(object).Assembly.Location, typeof(Enumerable).Assembly.Location }
            : trustedPlatformAssemblies.Split(
                Path.PathSeparator,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return paths
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToArray();
    }
}
