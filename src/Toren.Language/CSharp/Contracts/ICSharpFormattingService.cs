namespace Toren.Language.CSharp.Contracts;

public interface ICSharpFormattingService
{
    Task<string> FormatAsync(
        string sourceText,
        CancellationToken cancellationToken = default);
}
