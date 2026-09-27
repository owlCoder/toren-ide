using Toren.Core.Results;

namespace Toren.DotNet.Environment.Errors;

internal static class DotNetSdkResolutionErrors
{
    public static OperationError WorkingDirectoryUnavailable(string workingDirectory) =>
        OperationError.Create(
            "dotnet.sdk.resolve.directory-unavailable",
            $"The workspace directory is unavailable: {workingDirectory}");

    public static OperationError ResolutionFailed(int exitCode, string details) =>
        OperationError.Create(
            "dotnet.sdk.resolve.failed",
            $"dotnet --version exited with code {exitCode}. {details}");

    public static OperationError EmptyVersion() =>
        OperationError.Create(
            "dotnet.sdk.resolve.empty",
            "dotnet --version completed without returning an SDK version.");
}
