using Toren.Core.Results;
using Toren.Git.Models;

namespace Toren.Git.Contracts;

public interface IGitRepositoryService
{
    Task<Result<GitRepositoryStatus>> GetStatusAsync(
        string workingDirectory,
        CancellationToken cancellationToken = default);

    Task<Result<string>> GetDiffAsync(
        string workingDirectory,
        string? path = null,
        bool staged = false,
        CancellationToken cancellationToken = default);
}
