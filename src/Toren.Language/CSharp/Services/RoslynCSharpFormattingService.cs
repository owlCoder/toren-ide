using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Toren.Language.CSharp.Contracts;

namespace Toren.Language.CSharp.Services;

public sealed class RoslynCSharpFormattingService : ICSharpFormattingService
{
    public Task<string> FormatAsync(
        string sourceText,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceText);
        return Task.Run(
            () => Format(sourceText, cancellationToken),
            cancellationToken);
    }

    private static string Format(string sourceText, CancellationToken cancellationToken)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText, cancellationToken: cancellationToken);
        var root = syntaxTree.GetRoot(cancellationToken);
        var endOfLine = sourceText.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        return root.NormalizeWhitespace(indentation: "    ", eol: endOfLine).ToFullString();
    }
}
