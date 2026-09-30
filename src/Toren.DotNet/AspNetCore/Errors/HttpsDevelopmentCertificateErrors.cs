using Toren.Core.Results;

namespace Toren.DotNet.AspNetCore.Errors;

internal static class HttpsDevelopmentCertificateErrors
{
    public static OperationError ExecutionUnavailable(string details) =>
        OperationError.Create(
            "dotnet.https-certificate.execution.failed",
            $"Unable to execute the dotnet HTTPS development-certificate command. {details}");

    public static OperationError CommandFailed(string command, int exitCode, string details)
    {
        var message = string.IsNullOrWhiteSpace(details)
            ? $"dotnet dev-certs https {command} failed with exit code {exitCode}."
            : details.Trim();
        return OperationError.Create("dotnet.https-certificate.command.failed", message);
    }
}
