using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Toren.Language.CSharp.Contracts;
using Toren.Language.CSharp.Models;

namespace Toren.Language.CSharp.Services;

public sealed class RoslynCSharpDiagnosticService : ICSharpDiagnosticService, ICSharpWorkspaceDiagnosticService
{
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    public Task<IReadOnlyList<CSharpDiagnostic>> AnalyzeAsync(
        CSharpSemanticContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(context.ActiveDocumentPath))
        {
            return Task.FromResult<IReadOnlyList<CSharpDiagnostic>>([]);
        }

        return Task.Run(
            () => AnalyzeActiveDocumentAsync(context, cancellationToken),
            cancellationToken);
    }

    public Task<IReadOnlyList<CSharpDocumentDiagnostics>> AnalyzeDocumentsAsync(
        CSharpSemanticContext context,
        IReadOnlyList<string> documentPaths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(documentPaths);

        var normalizedPaths = documentPaths
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(PathComparer)
            .ToArray();
        if (normalizedPaths.Length == 0)
        {
            return Task.FromResult<IReadOnlyList<CSharpDocumentDiagnostics>>([]);
        }

        return Task.Run(
            () => AnalyzeCoreAsync(context, normalizedPaths, cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// Diagnostics for one document, computed without analyzing the rest of its project: the
    /// compiler binds only that document, and analyzers run on it alone. Analyzers that report
    /// when a whole compilation has been seen still run over the project, because their
    /// diagnostics for this document cannot be produced any other way.
    /// </summary>
    private static async Task<IReadOnlyList<CSharpDiagnostic>> AnalyzeActiveDocumentAsync(
        CSharpSemanticContext context,
        CancellationToken cancellationToken)
    {
        var compilationContext = RoslynCompilationContextFactory.Create(context, cancellationToken);
        if (compilationContext is null)
        {
            return [];
        }

        var compilation = compilationContext.Compilation;
        var tree = compilationContext.ActiveTree;
        var semanticModel = compilation.GetSemanticModel(tree);
        var diagnostics = new List<Diagnostic>(semanticModel.GetDiagnostics(cancellationToken: cancellationToken));

        var analyzers = RoslynAnalyzerLoader.Load(context.AnalyzerPaths);
        if (!analyzers.IsDefaultOrEmpty)
        {
            var options = CreateAnalyzerOptions(context);
            var documentAnalyzers = analyzers.Where(static analyzer => !ReportsAtCompilationEnd(analyzer)).ToImmutableArray();
            var compilationAnalyzers = analyzers.RemoveRange(documentAnalyzers);

            // The two groups are independent, so the project-wide one does not delay the other.
            var compilationDiagnostics = compilationAnalyzers.IsEmpty
                ? Task.FromResult(ImmutableArray<Diagnostic>.Empty)
                : compilation.WithAnalyzers(compilationAnalyzers, options).GetAnalyzerDiagnosticsAsync(cancellationToken);
            try
            {
                if (!documentAnalyzers.IsEmpty)
                {
                    var scoped = compilation.WithAnalyzers(documentAnalyzers, options);
                    diagnostics.AddRange(await scoped
                        .GetAnalyzerSyntaxDiagnosticsAsync(tree, cancellationToken)
                        .ConfigureAwait(false));
                    diagnostics.AddRange(await scoped
                        .GetAnalyzerSemanticDiagnosticsAsync(semanticModel, filterSpan: null, cancellationToken)
                        .ConfigureAwait(false));
                }
            }
            finally
            {
                // Awaited on every path so the project-wide run is never left unobserved.
                diagnostics.AddRange(await compilationDiagnostics.ConfigureAwait(false));
            }
        }

        var activePath = Path.GetFullPath(context.ActiveDocumentPath);
        var documentDiagnostics = new List<CSharpDiagnostic>();
        foreach (var diagnostic in diagnostics)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsReported(diagnostic, out var sourcePath) && PathComparer.Equals(sourcePath, activePath))
            {
                documentDiagnostics.Add(RoslynDiagnosticMapper.ToModel(diagnostic));
            }
        }

        return Order(documentDiagnostics);
    }

    private static bool ReportsAtCompilationEnd(DiagnosticAnalyzer analyzer)
    {
        try
        {
            return analyzer.SupportedDiagnostics.Any(static descriptor =>
                descriptor.CustomTags.Contains(WellKnownDiagnosticTags.CompilationEnd));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // An analyzer that cannot describe itself is given the whole compilation.
            return true;
        }
    }

    private static AnalyzerOptions CreateAnalyzerOptions(CSharpSemanticContext context) =>
        new(
            context.AdditionalFilePaths.Where(File.Exists)
                .Select(static path => (AdditionalText)new ProjectAdditionalText(path)).ToImmutableArray(),
            new ProjectAnalyzerConfigOptionsProvider(context.AnalyzerConfigPaths));

    private static bool IsReported(Diagnostic diagnostic, out string sourcePath)
    {
        if (diagnostic.Severity == DiagnosticSeverity.Hidden
            || !diagnostic.Location.IsInSource
            || diagnostic.Location.SourceTree is not { FilePath.Length: > 0 } sourceTree)
        {
            sourcePath = string.Empty;
            return false;
        }

        sourcePath = Path.GetFullPath(sourceTree.FilePath);
        return true;
    }

    private static CSharpDiagnostic[] Order(IEnumerable<CSharpDiagnostic> diagnostics) =>
        diagnostics
            .OrderBy(static diagnostic => diagnostic.StartLine)
            .ThenBy(static diagnostic => diagnostic.StartColumn)
            .ThenBy(static diagnostic => diagnostic.Id, StringComparer.Ordinal)
            .ToArray();

    private static async Task<IReadOnlyList<CSharpDocumentDiagnostics>> AnalyzeCoreAsync(
        CSharpSemanticContext context,
        string[] documentPaths,
        CancellationToken cancellationToken)
    {
        var compilationContext = RoslynCompilationContextFactory.Create(
            context, cancellationToken, reuseProjectState: false);
        if (compilationContext is null)
        {
            return documentPaths
                .Select(path => new CSharpDocumentDiagnostics(path, []))
                .ToArray();
        }

        var compilerDiagnostics = compilationContext.Compilation.GetDiagnostics(cancellationToken);
        var analyzers = RoslynAnalyzerLoader.Load(context.AnalyzerPaths);
        var analyzerDiagnostics = analyzers.IsDefaultOrEmpty
            ? []
            : await compilationContext.Compilation
                .WithAnalyzers(analyzers, CreateAnalyzerOptions(context))
                .GetAnalyzerDiagnosticsAsync(cancellationToken)
                .ConfigureAwait(false);

        var diagnosticsByPath = documentPaths.ToDictionary(
            static path => path,
            static _ => new List<CSharpDiagnostic>(),
            PathComparer);

        foreach (var diagnostic in compilerDiagnostics.Concat(analyzerDiagnostics))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsReported(diagnostic, out var sourcePath)
                && diagnosticsByPath.TryGetValue(sourcePath, out var documentDiagnostics))
            {
                documentDiagnostics.Add(RoslynDiagnosticMapper.ToModel(diagnostic));
            }
        }

        return documentPaths
            .Select(path => new CSharpDocumentDiagnostics(path, Order(diagnosticsByPath[path])))
            .ToArray();
    }
}
