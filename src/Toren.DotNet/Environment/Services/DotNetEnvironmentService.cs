using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.DotNet.Environment.Contracts;
using Toren.DotNet.Environment.Errors;
using Toren.DotNet.Environment.Models;
using Toren.DotNet.Environment.Parsing;

namespace Toren.DotNet.Environment.Services;

public sealed class DotNetEnvironmentService(IProcessRunner processRunner) : IDotNetEnvironmentService
{
    private readonly IProcessRunner _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));

    public async Task<Result<IReadOnlyList<DotNetSdkInfo>>> GetInstalledSdksAsync(
        CancellationToken cancellationToken = default)
    {
        var execution = await _processRunner.RunAsync(
            ProcessRequest.Create("dotnet", "--list-sdks"),
            cancellationToken).ConfigureAwait(false);

        if (execution.IsFailure)
        {
            return Result.Failure<IReadOnlyList<DotNetSdkInfo>>(
                DotNetEnvironmentErrors.ExecutableUnavailable(execution.Error.Message));
        }

        var processResult = execution.Value;
        if (!processResult.Succeeded)
        {
            var details = string.IsNullOrWhiteSpace(processResult.StandardError)
                ? "No additional error output was provided."
                : processResult.StandardError.Trim();

            return Result.Failure<IReadOnlyList<DotNetSdkInfo>>(
                DotNetEnvironmentErrors.SdkDiscoveryFailed(processResult.ExitCode, details));
        }

        return Result.Success<IReadOnlyList<DotNetSdkInfo>>(
            DotNetSdkListParser.Parse(processResult.StandardOutput));
    }
}
