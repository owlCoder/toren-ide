using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Toren.Language.CSharp.Contracts;
using Toren.Language.CSharp.Models;

namespace Toren.Language.CSharp.Services;

public sealed class RoslynCSharpDiagnosticService : ICSharpDiagnosticService
{
    public Task<IReadOnlyList<CSharpDiagnostic>> AnalyzeAsync(
        CSharpSemanticContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Task.Run(
            () => AnalyzeCoreAsync(context, cancellationToken),
            cancellationToken);
    }

    private static async Task<IReadOnlyList<CSharpDiagnostic>> AnalyzeCoreAsync(
        CSharpSemanticContext context,
        CancellationToken cancellationToken)
    {
        var compilationContext = RoslynCompilationContextFactory.Create(context, cancellationToken);
        if (compilationContext is null)
        {
            return [];
        }

        var compilerDiagnostics = compilationContext.Compilation.GetDiagnostics(cancellationToken);
        var analyzers = RoslynAnalyzerLoader.Load(context.AnalyzerPaths);
        var analyzerDiagnostics = analyzers.IsDefaultOrEmpty
            ? []
            : await compilationContext.Compilation
                .WithAnalyzers(analyzers)
                .GetAnalyzerDiagnosticsAsync(cancellationToken)
                .ConfigureAwait(false);

        return compilerDiagnostics
            .Concat(analyzerDiagnostics)
            .Where(diagnostic =>
                diagnostic.Severity != DiagnosticSeverity.Hidden
                && diagnostic.Location.IsInSource
                && ReferenceEquals(diagnostic.Location.SourceTree, compilationContext.ActiveTree))
            .Select(RoslynDiagnosticMapper.ToModel)
            .OrderBy(diagnostic => diagnostic.StartLine)
            .ThenBy(diagnostic => diagnostic.StartColumn)
            .ThenBy(diagnostic => diagnostic.Id, StringComparer.Ordinal)
            .ToArray();
    }
}
