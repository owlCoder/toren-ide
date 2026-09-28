namespace Toren.Language.CSharp.Contracts;

public interface ICSharpFormattingService
{
    Task<string> FormatAsync(
        string sourceText,
        CancellationToken cancellationToken = default);

    Task<string> FormatSelectionAsync(
        string sourceText,
        int startOffset,
        int length,
        CancellationToken cancellationToken = default);
}
