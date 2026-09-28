using Microsoft.CodeAnalysis;
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

        return Task.Run<IReadOnlyList<CSharpDiagnostic>>(
            () => Analyze(context, cancellationToken),
            cancellationToken);
    }

    private static CSharpDiagnostic[] Analyze(
        CSharpSemanticContext context,
        CancellationToken cancellationToken)
    {
        var compilationContext = RoslynCompilationContextFactory.Create(context, cancellationToken);
        if (compilationContext is null)
        {
            return [];
        }

        return compilationContext.Compilation
            .GetDiagnostics(cancellationToken)
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
