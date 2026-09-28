using System.Globalization;
using Microsoft.CodeAnalysis;
using Toren.Language.CSharp.Models;

namespace Toren.Language.CSharp.Services;

internal static class RoslynDiagnosticMapper
{
    public static CSharpDiagnostic ToModel(Diagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);

        var span = diagnostic.Location.GetLineSpan().Span;
        return new CSharpDiagnostic(
            diagnostic.Id,
            diagnostic.GetMessage(CultureInfo.InvariantCulture),
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
