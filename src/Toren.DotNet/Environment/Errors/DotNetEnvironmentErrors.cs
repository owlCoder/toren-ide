using Toren.Core.Results;

namespace Toren.DotNet.Environment.Errors;

internal static class DotNetEnvironmentErrors
{
    public static OperationError ExecutableUnavailable(string details)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(details);

        return OperationError.Create(
            "dotnet.executable.unavailable",
            $"The dotnet executable could not be started. Ensure a supported .NET SDK is installed and available on PATH. {details}");
    }

    public static OperationError SdkDiscoveryFailed(int exitCode, string details)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(details);

        return OperationError.Create(
            "dotnet.sdk.discovery.failed",
            $"dotnet --list-sdks exited with code {exitCode}. {details}");
    }
}
