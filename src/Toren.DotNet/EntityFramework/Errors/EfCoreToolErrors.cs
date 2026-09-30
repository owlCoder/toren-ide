using Toren.Core.Results;

namespace Toren.DotNet.EntityFramework.Errors;

internal static class EfCoreToolErrors
{
    public static OperationError ProjectUnavailable(string path) =>
        OperationError.Create(
            "dotnet.ef.project.unavailable",
            $"The EF Core project does not exist: {path}");

    public static OperationError ExecutionUnavailable(string details) =>
        OperationError.Create(
            "dotnet.ef.execution.failed",
            $"Unable to execute the dotnet ef command. {details}");

    public static OperationError CommandFailed(string command, int exitCode, string details)
    {
        var message = string.IsNullOrWhiteSpace(details)
            ? $"dotnet ef {command} failed with exit code {exitCode}."
            : details.Trim();
        return OperationError.Create("dotnet.ef.command.failed", message);
    }

    public static OperationError InvalidMigrationOutput(string details) =>
        OperationError.Create(
            "dotnet.ef.migrations.output.invalid",
            $"Unable to parse EF Core migration output. {details}");
}
