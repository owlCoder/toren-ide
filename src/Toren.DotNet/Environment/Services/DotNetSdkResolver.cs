using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.DotNet.Environment.Contracts;
using Toren.DotNet.Environment.Errors;

namespace Toren.DotNet.Environment.Services;

public sealed class DotNetSdkResolver(IProcessRunner processRunner) : IDotNetSdkResolver
{
    private readonly IProcessRunner _processRunner = processRunner
        ?? throw new ArgumentNullException(nameof(processRunner));

    public async Task<Result<string>> ResolveVersionAsync(
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(workingDirectory))
        {
            return Result.Failure<string>(
                DotNetSdkResolutionErrors.WorkingDirectoryUnavailable(workingDirectory));
        }

        var request = ProcessRequest.Create("dotnet", "--version") with
        {
            WorkingDirectory = workingDirectory,
        };
        var execution = await _processRunner.RunAsync(request, cancellationToken).ConfigureAwait(false);

        if (!execution.IsSuccess)
        {
            return Result.Failure<string>(
                DotNetEnvironmentErrors.ExecutableUnavailable(execution.Error.Message));
        }

        if (!execution.Value.Succeeded)
        {
            var details = string.IsNullOrWhiteSpace(execution.Value.StandardError)
                ? execution.Value.StandardOutput.Trim()
                : execution.Value.StandardError.Trim();
            if (string.IsNullOrWhiteSpace(details))
            {
                details = "No additional error output was provided.";
            }

            return Result.Failure<string>(
                DotNetSdkResolutionErrors.ResolutionFailed(execution.Value.ExitCode, details));
        }

        var version = execution.Value.StandardOutput.Trim();
        return string.IsNullOrWhiteSpace(version)
            ? Result.Failure<string>(DotNetSdkResolutionErrors.EmptyVersion())
            : Result.Success(version);
    }
}
