using Toren.Core.Results;
using Toren.DotNet.Execution.Models;

namespace Toren.DotNet.Execution.Errors;

internal static class DotNetCommandErrors
{
    public static OperationError WorkingDirectoryUnavailable(string workingDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        return OperationError.Create(
            "dotnet.command.working-directory.unavailable",
            $"The .NET command working directory does not exist: {workingDirectory}");
    }

    public static OperationError ExecutionUnavailable(DotNetCommandKind kind, string details)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(details);

        return OperationError.Create(
            "dotnet.command.start.failed",
            $"Unable to start the dotnet command for {kind}. {details}");
    }
}
