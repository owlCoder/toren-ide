using Toren.Core.Results;

namespace Toren.DotNet.Packages.Errors;

internal static class DotNetPackageErrors
{
    public static OperationError WorkingDirectoryUnavailable(string path) =>
        OperationError.Create(
            "dotnet.package.working-directory.unavailable",
            $"The package working directory does not exist: {path}");

    public static OperationError ProjectUnavailable(string path) =>
        OperationError.Create(
            "dotnet.package.project.unavailable",
            $"The package project does not exist: {path}");

    public static OperationError ExecutionUnavailable(string details) =>
        OperationError.Create(
            "dotnet.package.execution.failed",
            $"Unable to execute the dotnet package command. {details}");

    public static OperationError CommandFailed(string command, int exitCode, string details)
    {
        var message = string.IsNullOrWhiteSpace(details)
            ? $"dotnet package {command} failed with exit code {exitCode}."
            : details.Trim();
        return OperationError.Create("dotnet.package.command.failed", message);
    }

    public static OperationError InvalidOutput(string command, string details) =>
        OperationError.Create(
            "dotnet.package.output.invalid",
            $"Unable to parse dotnet package {command} output. {details}");
}
