using Toren.Core.Results;

namespace Toren.DotNet.UserSecrets.Errors;

internal static class DotNetUserSecretsErrors
{
    public static OperationError ProjectUnavailable(string path) =>
        OperationError.Create(
            "dotnet.user-secrets.project.unavailable",
            $"The user-secrets project does not exist: {path}");

    public static OperationError ExecutionUnavailable(string details) =>
        OperationError.Create(
            "dotnet.user-secrets.execution.failed",
            $"Unable to execute the dotnet user-secrets command. {details}");

    public static OperationError CommandFailed(string command, int exitCode, string details)
    {
        var message = string.IsNullOrWhiteSpace(details)
            ? $"dotnet user-secrets {command} failed with exit code {exitCode}."
            : details.Trim();
        return OperationError.Create("dotnet.user-secrets.command.failed", message);
    }
}
