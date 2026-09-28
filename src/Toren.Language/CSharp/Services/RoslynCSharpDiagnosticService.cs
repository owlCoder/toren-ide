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

    public async Task<IReadOnlyList<CSharpDiagnostic>> AnalyzeAsync(
        CSharpSemanticContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var diagnostics = await AnalyzeDocumentsAsync(
                context,
                [context.ActiveDocumentPath],
                cancellationToken)
            .ConfigureAwait(false);
        return diagnostics.Count == 0
            ? []
            : diagnostics[0].Diagnostics;
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

    private static async Task<IReadOnlyList<CSharpDocumentDiagnostics>> AnalyzeCoreAsync(
        CSharpSemanticContext context,
        string[] documentPaths,
        CancellationToken cancellationToken)
    {
        var compilationContext = RoslynCompilationContextFactory.Create(context, cancellationToken);
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
                .WithAnalyzers(analyzers)
                .GetAnalyzerDiagnosticsAsync(cancellationToken)
                .ConfigureAwait(false);

        var diagnosticsByPath = documentPaths.ToDictionary(
            static path => path,
            static _ => new List<CSharpDiagnostic>(),
            PathComparer);

        foreach (var diagnostic in compilerDiagnostics.Concat(analyzerDiagnostics))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (diagnostic.Severity == DiagnosticSeverity.Hidden
                || !diagnostic.Location.IsInSource
                || diagnostic.Location.SourceTree is not { FilePath.Length: > 0 } sourceTree)
            {
                continue;
            }

            var sourcePath = Path.GetFullPath(sourceTree.FilePath);
            if (diagnosticsByPath.TryGetValue(sourcePath, out var documentDiagnostics))
            {
                documentDiagnostics.Add(RoslynDiagnosticMapper.ToModel(diagnostic));
            }
        }

        return documentPaths
            .Select(path => new CSharpDocumentDiagnostics(
                path,
                diagnosticsByPath[path]
                    .OrderBy(diagnostic => diagnostic.StartLine)
                    .ThenBy(diagnostic => diagnostic.StartColumn)
                    .ThenBy(diagnostic => diagnostic.Id, StringComparer.Ordinal)
                    .ToArray()))
            .ToArray();
    }
}
