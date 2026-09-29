using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.Git.Contracts;
using Toren.Git.Models;
using Toren.Git.Parsers;

namespace Toren.Git.Services;

public sealed class GitRepositoryService(IProcessRunner processRunner) : IGitRepositoryService
{
    private const string GitCommandFailedErrorCode = "git.command.failed";
    private readonly IProcessRunner _processRunner = processRunner
        ?? throw new ArgumentNullException(nameof(processRunner));

    public async Task<Result<GitRepositoryStatus>> GetStatusAsync(
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        var request = new ProcessRequest(
            "git",
            ["-C", Path.GetFullPath(workingDirectory), "status", "--porcelain=v2", "--branch", "--untracked-files=all", "-z"]);
        var result = await _processRunner.RunAsync(request, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return Result.Failure<GitRepositoryStatus>(result.Error);
        }

        var processResult = result.Value!;
        if (!processResult.Succeeded)
        {
            return Result.Failure<GitRepositoryStatus>(CreateCommandError("status", processResult));
        }

        return Result.Success(GitStatusParser.Parse(processResult.StandardOutput));
    }

    public async Task<Result<string>> GetDiffAsync(
        string workingDirectory,
        string? path = null,
        bool staged = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        var arguments = new List<string>
        {
            "-C",
            Path.GetFullPath(workingDirectory),
            "diff",
            "--no-ext-diff",
            "--no-color",
        };
        if (staged)
        {
            arguments.Add("--cached");
        }

        if (!string.IsNullOrWhiteSpace(path))
        {
            arguments.Add("--");
            arguments.Add(path);
        }

        var result = await _processRunner
            .RunAsync(new ProcessRequest("git", arguments), cancellationToken)
            .ConfigureAwait(false);
        if (result.IsFailure)
        {
            return Result.Failure<string>(result.Error);
        }

        var processResult = result.Value!;
        return processResult.Succeeded
            ? Result.Success(processResult.StandardOutput)
            : Result.Failure<string>(CreateCommandError("diff", processResult));
    }

    public Task<Result<bool>> StageAsync(
        string workingDirectory,
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return RunMutationAsync(
            workingDirectory,
            "stage",
            ["add", "--", path],
            cancellationToken);
    }

    public Task<Result<bool>> UnstageAsync(
        string workingDirectory,
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return RunMutationAsync(
            workingDirectory,
            "unstage",
            ["restore", "--staged", "--", path],
            cancellationToken);
    }

    public Task<Result<bool>> CommitAsync(
        string workingDirectory,
        string message,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return RunMutationAsync(
            workingDirectory,
            "commit",
            ["commit", "--message", message.Trim()],
            cancellationToken);
    }

    public async Task<Result<IReadOnlyList<GitBranchInfo>>> GetBranchesAsync(
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        var request = new ProcessRequest(
            "git",
            [
                "-C",
                Path.GetFullPath(workingDirectory),
                "for-each-ref",
                "--format=%(refname:short)%00%(HEAD)%00%(upstream:short)%00",
                "refs/heads/",
            ]);
        var result = await _processRunner.RunAsync(request, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return Result.Failure<IReadOnlyList<GitBranchInfo>>(result.Error);
        }

        var processResult = result.Value!;
        return processResult.Succeeded
            ? Result.Success(GitBranchParser.Parse(processResult.StandardOutput))
            : Result.Failure<IReadOnlyList<GitBranchInfo>>(CreateCommandError("branch list", processResult));
    }

    public Task<Result<bool>> SwitchBranchAsync(
        string workingDirectory,
        string branchName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);
        return RunMutationAsync(
            workingDirectory,
            "switch",
            ["switch", branchName],
            cancellationToken);
    }

    public Task<Result<bool>> CreateBranchAsync(
        string workingDirectory,
        string branchName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);
        return RunMutationAsync(
            workingDirectory,
            "branch create",
            ["switch", "-c", branchName],
            cancellationToken);
    }

    public Task<Result<bool>> FetchAsync(
        string workingDirectory,
        CancellationToken cancellationToken = default) =>
        RunMutationAsync(
            workingDirectory,
            "fetch",
            ["fetch", "--prune"],
            cancellationToken);

    public Task<Result<bool>> PullAsync(
        string workingDirectory,
        CancellationToken cancellationToken = default) =>
        RunMutationAsync(
            workingDirectory,
            "pull",
            ["pull", "--ff-only"],
            cancellationToken);

    public Task<Result<bool>> PushAsync(
        string workingDirectory,
        CancellationToken cancellationToken = default) =>
        RunMutationAsync(
            workingDirectory,
            "push",
            ["push"],
            cancellationToken);

    private async Task<Result<bool>> RunMutationAsync(
        string workingDirectory,
        string command,
        IReadOnlyList<string> commandArguments,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        var arguments = new List<string>(commandArguments.Count + 2)
        {
            "-C",
            Path.GetFullPath(workingDirectory),
        };
        arguments.AddRange(commandArguments);

        var result = await _processRunner
            .RunAsync(new ProcessRequest("git", arguments), cancellationToken)
            .ConfigureAwait(false);
        if (result.IsFailure)
        {
            return Result.Failure<bool>(result.Error);
        }

        var processResult = result.Value!;
        return processResult.Succeeded
            ? Result.Success(true)
            : Result.Failure<bool>(CreateCommandError(command, processResult));
    }

    private static OperationError CreateCommandError(string command, ProcessResult result)
    {
        var message = string.IsNullOrWhiteSpace(result.StandardError)
            ? $"Git {command} failed with exit code {result.ExitCode}."
            : result.StandardError.Trim();
        return OperationError.Create(GitCommandFailedErrorCode, message);
    }
}
