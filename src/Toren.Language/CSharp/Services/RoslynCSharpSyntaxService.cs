using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Toren.Language.CSharp.Contracts;
using Toren.Language.CSharp.Models;

namespace Toren.Language.CSharp.Services;

public sealed class RoslynCSharpSyntaxService : ICSharpSyntaxService
{
    public Task<IReadOnlyList<CSharpDiagnostic>> AnalyzeAsync(
        string sourceText,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceText);
        return Task.Run(() => Analyze(sourceText, cancellationToken), cancellationToken);
    }

    private static IReadOnlyList<CSharpDiagnostic> Analyze(
        string sourceText,
        CancellationToken cancellationToken)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText, cancellationToken: cancellationToken);
        return syntaxTree
            .GetDiagnostics(cancellationToken)
            .Where(diagnostic => diagnostic.Location.IsInSource)
            .Select(ToModel)
            .ToArray();
    }

    private static CSharpDiagnostic ToModel(Diagnostic diagnostic)
    {
        var span = diagnostic.Location.GetLineSpan().Span;
        return new CSharpDiagnostic(
            diagnostic.Id,
            diagnostic.GetMessage(),
            diagnostic.Severity switch
            {
                DiagnosticSeverity.Error => CSharpDiagnosticSeverity.Error,
                DiagnosticSeverity.Warning => CSharpDiagnosticSeverity.Warning,
                _ => CSharpDiagnosticSeverity.Info,
            },
            span.Start.Line + 1,
            span.Start.Character + 1,
            span.End.Line + 1,
            span.End.Character + 1);
    }
}
