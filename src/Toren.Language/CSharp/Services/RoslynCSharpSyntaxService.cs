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
        return Task.Run<IReadOnlyList<CSharpDiagnostic>>(
            () => Analyze(sourceText, cancellationToken),
            cancellationToken);
    }

    private static CSharpDiagnostic[] Analyze(
        string sourceText,
        CancellationToken cancellationToken)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText, cancellationToken: cancellationToken);
        return syntaxTree
            .GetDiagnostics(cancellationToken)
            .Where(diagnostic => diagnostic.Location.IsInSource)
            .Select(RoslynDiagnosticMapper.ToModel)
            .ToArray();
    }
}
