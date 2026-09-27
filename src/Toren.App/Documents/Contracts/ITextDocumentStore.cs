using Toren.App.Documents.Models;
using Toren.Core.Results;

namespace Toren.App.Documents.Contracts;

public interface ITextDocumentStore
{
    Task<Result<TextDocumentContent>> LoadAsync(
        string path,
        CancellationToken cancellationToken = default);

    Task<Result<TextDocumentContent>> SaveAsync(
        TextDocumentContent document,
        CancellationToken cancellationToken = default);
}
