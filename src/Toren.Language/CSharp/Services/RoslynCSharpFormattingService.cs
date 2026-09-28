using Microsoft.CodeAnalysis.Formatting;
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

        var context = new CSharpSemanticContext(
            FormatDocumentPath,
            [new CSharpSourceDocument(FormatDocumentPath, sourceText)]);
        using var roslynContext = RoslynWorkspaceContextFactory.Create(context, cancellationToken);
        if (roslynContext is null)
        {
            return sourceText;
        }

        var formattedDocument = await Formatter
            .FormatAsync(roslynContext.ActiveDocument, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var formattedText = await formattedDocument
            .GetTextAsync(cancellationToken)
            .ConfigureAwait(false);
        return formattedText.ToString();
    }
}
