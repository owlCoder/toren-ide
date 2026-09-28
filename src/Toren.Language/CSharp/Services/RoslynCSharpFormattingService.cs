using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using Toren.Language.CSharp.Contracts;
using Toren.Language.CSharp.Models;

namespace Toren.Language.CSharp.Services;

public sealed class RoslynCSharpFormattingService : ICSharpFormattingService
{
    private const string FormatDocumentPath = "__toren_format__.cs";

    public async Task<string> FormatAsync(
        string sourceText,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceText);

        var lineEnding = DetectLineEnding(sourceText);
        var syntaxTree = CSharpSyntaxTree.ParseText(
            sourceText,
            path: FormatDocumentPath,
            cancellationToken: cancellationToken);
        var root = await syntaxTree.GetRootAsync(cancellationToken).ConfigureAwait(false);
        return root
            .NormalizeWhitespace(indentation: "    ", eol: lineEnding, elasticTrivia: false)
            .ToFullString();
    }

    public async Task<string> FormatSelectionAsync(
        string sourceText,
        int startOffset,
        int length,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceText);
        ArgumentOutOfRangeException.ThrowIfNegative(startOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        if (startOffset > sourceText.Length || length > sourceText.Length - startOffset)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        if (length == 0)
        {
            return sourceText;
        }

        using var roslynContext = CreateWorkspaceContext(sourceText, cancellationToken);
        if (roslynContext is null)
        {
            return sourceText;
        }

        var formattedDocument = await Formatter
            .FormatAsync(
                roslynContext.ActiveDocument,
                [new TextSpan(startOffset, length)],
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var formatted = await GetDocumentTextAsync(formattedDocument, cancellationToken).ConfigureAwait(false);
        return NormalizeLineEndings(formatted, DetectLineEnding(sourceText));
    }

    private static RoslynWorkspaceContext? CreateWorkspaceContext(
        string sourceText,
        CancellationToken cancellationToken)
    {
        var context = new CSharpSemanticContext(
            FormatDocumentPath,
            [new CSharpSourceDocument(FormatDocumentPath, sourceText)]);
        return RoslynWorkspaceContextFactory.Create(context, cancellationToken);
    }

    private static async Task<string> GetDocumentTextAsync(
        Document document,
        CancellationToken cancellationToken)
    {
        var formattedText = await document
            .GetTextAsync(cancellationToken)
            .ConfigureAwait(false);
        return formattedText.ToString();
    }

    private static string DetectLineEnding(string sourceText)
    {
        if (sourceText.Contains("\r\n", StringComparison.Ordinal))
        {
            return "\r\n";
        }

        return sourceText.Contains('\r', StringComparison.Ordinal)
            ? "\r"
            : "\n";
    }

    private static string NormalizeLineEndings(string text, string lineEnding)
    {
        var normalized = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        return lineEnding.Equals("\n", StringComparison.Ordinal)
            ? normalized
            : normalized.Replace("\n", lineEnding, StringComparison.Ordinal);
    }
}
