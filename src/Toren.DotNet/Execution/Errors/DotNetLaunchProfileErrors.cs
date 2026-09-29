using Toren.Core.Results;

namespace Toren.DotNet.Execution.Errors;

internal static class DotNetLaunchProfileErrors
{
    public static OperationError ReadFailed(string path, string details)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(details);

        return OperationError.Create(
            "dotnet.launch-profile.read.failed",
            $"Unable to read launch profiles from '{path}'. {details}");
    }

    public static OperationError ParseFailed(string path, string details)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(details);

        return OperationError.Create(
            "dotnet.launch-profile.parse.failed",
            $"Unable to parse launch profiles from '{path}'. {details}");
    }
}
