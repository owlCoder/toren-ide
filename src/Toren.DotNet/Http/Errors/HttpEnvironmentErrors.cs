using Toren.Core.Results;

namespace Toren.DotNet.Http.Errors;

internal static class HttpEnvironmentErrors
{
    public static OperationError ReadFailed(string path, string details) =>
        OperationError.Create(
            "http.environment.read.failed",
            $"Failed to read HTTP environment file '{path}'. {details}");

    public static OperationError ParseFailed(string path, string details) =>
        OperationError.Create(
            "http.environment.parse.failed",
            $"Failed to parse HTTP environment file '{path}'. {details}");
}
