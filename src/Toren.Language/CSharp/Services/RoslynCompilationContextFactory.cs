using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Toren.Language.CSharp.Models;

namespace Toren.Language.CSharp.Services;

internal static class RoslynCompilationContextFactory
{
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    public static RoslynCompilationContext? Create(
        CSharpSemanticContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(context.ActiveDocumentPath);
        ArgumentNullException.ThrowIfNull(context.Documents);

        if (context.Documents.Count == 0)
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

        var compilation = CSharpCompilation.Create(
            "Toren.SemanticAnalysis",
            syntaxTrees,
            RoslynMetadataReferenceProvider.GetReferences(context.MetadataReferencePaths),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        return new RoslynCompilationContext(
            compilation,
            activeTree,
            compilation.GetSemanticModel(activeTree, ignoreAccessibility: true),
            activeTree.GetText(cancellationToken));
    }
}

internal sealed record RoslynCompilationContext(
    CSharpCompilation Compilation,
    SyntaxTree ActiveTree,
    SemanticModel SemanticModel,
    SourceText SourceText);
