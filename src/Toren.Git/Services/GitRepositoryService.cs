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

    private static OperationError CreateCommandError(string command, ProcessResult result)
    {
        var message = string.IsNullOrWhiteSpace(result.StandardError)
            ? $"Git {command} failed with exit code {result.ExitCode}."
            : result.StandardError.Trim();
        return OperationError.Create(GitCommandFailedErrorCode, message);
    }
}
