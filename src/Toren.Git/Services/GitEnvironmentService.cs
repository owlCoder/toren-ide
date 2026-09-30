using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.Git.Contracts;

namespace Toren.Git.Services;

public sealed class GitEnvironmentService(IProcessRunner processRunner) : IGitEnvironmentService
{
    private const string GitUnavailableErrorCode = "git.environment.unavailable";
    private readonly IProcessRunner _processRunner = processRunner
        ?? throw new ArgumentNullException(nameof(processRunner));

    public async Task<Result<string>> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        var result = await _processRunner
            .RunAsync(new ProcessRequest("git", ["--version"]), cancellationToken)
            .ConfigureAwait(false);
        if (result.IsFailure)
        {
            return Result.Failure<string>(result.Error);
        }

        var processResult = result.Value!;
        if (!processResult.Succeeded)
        {
            var message = string.IsNullOrWhiteSpace(processResult.StandardError)
                ? "Git is not available on PATH."
                : processResult.StandardError.Trim();
            return Result.Failure<string>(OperationError.Create(GitUnavailableErrorCode, message));
        }

        var version = processResult.StandardOutput.Trim();
        return string.IsNullOrWhiteSpace(version)
            ? Result.Failure<string>(OperationError.Create(
                GitUnavailableErrorCode,
                "Git did not report a version."))
            : Result.Success(version);
    }
}
