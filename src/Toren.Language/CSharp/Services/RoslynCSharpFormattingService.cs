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

        using var roslynContext = CreateWorkspaceContext(sourceText, cancellationToken);
        if (roslynContext is null)
        {
            return sourceText;
        }

        var formattedDocument = await Formatter
            .FormatAsync(roslynContext.ActiveDocument, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return await GetDocumentTextAsync(formattedDocument, cancellationToken).ConfigureAwait(false);
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
        return await GetDocumentTextAsync(formattedDocument, cancellationToken).ConfigureAwait(false);
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
        Microsoft.CodeAnalysis.Document document,
        CancellationToken cancellationToken)
    {
        var formattedText = await document
            .GetTextAsync(cancellationToken)
            .ConfigureAwait(false);
        return formattedText.ToString();
    }
}
