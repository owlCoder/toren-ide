using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.DotNet.AspNetCore.Contracts;
using Toren.DotNet.AspNetCore.Errors;
using Toren.DotNet.AspNetCore.Models;

namespace Toren.DotNet.AspNetCore.Services;

public sealed class HttpsDevelopmentCertificateService(IProcessRunner processRunner)
    : IHttpsDevelopmentCertificateService
{
    private readonly IProcessRunner _processRunner = processRunner
        ?? throw new ArgumentNullException(nameof(processRunner));

    public async Task<Result<HttpsDevelopmentCertificateState>> CheckAsync(
        CancellationToken cancellationToken = default)
    {
        var trustedCheck = await RunAsync(
            ["dev-certs", "https", "--check", "--trust"],
            cancellationToken).ConfigureAwait(false);
        if (trustedCheck.IsFailure)
        {
            return Result.Failure<HttpsDevelopmentCertificateState>(trustedCheck.Error);
        }

        if (trustedCheck.Value!.Succeeded)
        {
            return Result.Success(HttpsDevelopmentCertificateState.Trusted);
        }

        var validityCheck = await RunAsync(
            ["dev-certs", "https", "--check"],
            cancellationToken).ConfigureAwait(false);
        if (validityCheck.IsFailure)
        {
            return Result.Failure<HttpsDevelopmentCertificateState>(validityCheck.Error);
        }

        return Result.Success(
            validityCheck.Value!.Succeeded
                ? HttpsDevelopmentCertificateState.ValidUntrusted
                : HttpsDevelopmentCertificateState.Missing);
    }

    public async Task<Result<bool>> TrustAsync(CancellationToken cancellationToken = default)
    {
        var execution = await RunAsync(
            ["dev-certs", "https", "--trust"],
            cancellationToken).ConfigureAwait(false);
        if (execution.IsFailure)
        {
            return Result.Failure<bool>(execution.Error);
        }

        var processResult = execution.Value!;
        return processResult.Succeeded
            ? Result.Success(true)
            : Result.Failure<bool>(
                HttpsDevelopmentCertificateErrors.CommandFailed(
                    "--trust",
                    processResult.ExitCode,
                    processResult.StandardError));
    }

    private async Task<Result<ProcessResult>> RunAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var execution = await _processRunner
            .RunAsync(new ProcessRequest("dotnet", arguments), cancellationToken)
            .ConfigureAwait(false);
        return execution.IsSuccess
            ? execution
            : Result.Failure<ProcessResult>(
                HttpsDevelopmentCertificateErrors.ExecutionUnavailable(execution.Error.Message));
    }
}
