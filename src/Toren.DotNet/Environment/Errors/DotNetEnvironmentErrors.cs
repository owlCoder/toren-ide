using Toren.Core.Results;

namespace Toren.DotNet.Environment.Errors;

internal static class DotNetEnvironmentErrors
{
    public static Error ExecutableUnavailable(string details)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(details);

        return Error.Create(
            "dotnet.executable.unavailable",
            $"The dotnet executable could not be started. Ensure a supported .NET SDK is installed and available on PATH. {details}");
    }

    public static Error SdkDiscoveryFailed(int exitCode, string details)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(details);

        return Error.Create(
            "dotnet.sdk.discovery.failed",
            $"dotnet --list-sdks exited with code {exitCode}. {details}");
    }
}
