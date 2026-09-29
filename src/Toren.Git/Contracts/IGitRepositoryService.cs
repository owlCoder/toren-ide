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

    Task<Result<IReadOnlyList<GitBranchInfo>>> GetBranchesAsync(
        string workingDirectory,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> SwitchBranchAsync(
        string workingDirectory,
        string branchName,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> CreateBranchAsync(
        string workingDirectory,
        string branchName,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> FetchAsync(
        string workingDirectory,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> PullAsync(
        string workingDirectory,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> PushAsync(
        string workingDirectory,
        CancellationToken cancellationToken = default);
}
