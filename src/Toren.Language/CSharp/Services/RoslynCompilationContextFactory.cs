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

    /// <param name="context">The documents and compiler inputs to analyze.</param>
    /// <param name="cancellationToken">Cancels parsing and generator execution.</param>
    /// <param name="reuseProjectState">
    /// Whether to reuse and update the project's cached trees and generator state. Passes over
    /// a whole workspace turn this off so they neither retain every project nor displace the
    /// projects being edited.
    /// </param>
    public static RoslynCompilationContext? Create(
        CSharpSemanticContext context,
        CancellationToken cancellationToken,
        bool reuseProjectState = true)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(context.ActiveDocumentPath);
        ArgumentNullException.ThrowIfNull(context.Documents);

        if (context.Documents.Count == 0)
        {
            return null;
        }

        var languageVersion = LanguageVersionFacts.TryParse(context.LanguageVersion ?? "default", out var parsed)
            ? parsed : LanguageVersion.Default;
        var parseOptions = new CSharpParseOptions(languageVersion, preprocessorSymbols: context.DefineConstants);
        var nullable = context.Nullable?.ToLowerInvariant() switch
        {
            "enable" => NullableContextOptions.Enable,
            "annotations" => NullableContextOptions.Annotations,
            "warnings" => NullableContextOptions.Warnings,
            _ => NullableContextOptions.Disable,
        };
        var outputKind = context.OutputType?.ToLowerInvariant() switch
        {
            "exe" => OutputKind.ConsoleApplication,
            "winexe" => OutputKind.WindowsApplication,
            _ => OutputKind.DynamicallyLinkedLibrary,
        };
        var projectState = reuseProjectState ? RoslynProjectState.For(context.ProjectPath) : null;
        var syntaxTrees = projectState is not null
            ? projectState.GetSyntaxTrees(context.Documents, parseOptions, cancellationToken)
            : context.Documents
                .Select(document => CSharpSyntaxTree.ParseText(
                    document.Text,
                    options: parseOptions,
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
            new CSharpCompilationOptions(outputKind, allowUnsafe: context.AllowUnsafe, nullableContextOptions: nullable));
        if (projectState is not null)
        {
            compilation = projectState.RunGenerators(compilation, context, parseOptions, cancellationToken);
        }
        else if (RoslynAnalyzerLoader.LoadGenerators(context.AnalyzerPaths) is { IsDefaultOrEmpty: false } generators)
        {
            var additionalTexts = context.AdditionalFilePaths.Where(File.Exists)
                .Select(static path => (AdditionalText)new ProjectAdditionalText(path)).ToArray();
            GeneratorDriver driver = CSharpGeneratorDriver.Create(generators, additionalTexts, parseOptions,
                new ProjectAnalyzerConfigOptionsProvider(context.AnalyzerConfigPaths));
            driver.RunGeneratorsAndUpdateCompilation(compilation, out var generatedCompilation, out _, cancellationToken);
            compilation = (CSharpCompilation)generatedCompilation;
        }

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
