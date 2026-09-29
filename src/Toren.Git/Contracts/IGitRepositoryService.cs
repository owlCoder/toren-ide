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

    Task<Result<bool>> StageAsync(
        string workingDirectory,
        string path,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> UnstageAsync(
        string workingDirectory,
        string path,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> CommitAsync(
        string workingDirectory,
        string message,
        CancellationToken cancellationToken = default);
}
