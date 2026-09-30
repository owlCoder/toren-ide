using Toren.Core.Results;
using Toren.DotNet.UserSecrets.Models;

namespace Toren.DotNet.UserSecrets.Contracts;

public interface IDotNetUserSecretsService
{
    Task<Result<bool>> InitializeAsync(string projectPath, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<UserSecretEntry>>> ListAsync(
        string projectPath,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> SetAsync(
        string projectPath,
        string key,
        string value,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> RemoveAsync(
        string projectPath,
        string key,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> ClearAsync(string projectPath, CancellationToken cancellationToken = default);
}
