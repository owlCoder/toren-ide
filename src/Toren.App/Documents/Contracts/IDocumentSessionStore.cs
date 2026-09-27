using Toren.App.Documents.Models;
using Toren.Core.Results;

namespace Toren.App.Documents.Contracts;

public interface IDocumentSessionStore
{
    Task<Result<DocumentSessionState>> LoadAsync(CancellationToken cancellationToken = default);

    Task<Result<DocumentSessionState>> SaveAsync(
        DocumentSessionState session,
        CancellationToken cancellationToken = default);
}
