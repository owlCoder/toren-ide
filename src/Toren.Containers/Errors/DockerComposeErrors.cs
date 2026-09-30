using Toren.Core.Results;

namespace Toren.Containers.Errors;

internal static class DockerComposeErrors
{
    public static OperationError ComposeFileUnavailable(string path) =>
        OperationError.Create(
            "docker.compose.file.unavailable",
            $"The Docker Compose file does not exist: {path}");

    public static OperationError ExecutionUnavailable(string details) =>
        OperationError.Create(
            "docker.compose.execution.failed",
            $"Unable to execute Docker Compose. {details}");

    public static OperationError CommandFailed(string command, int exitCode, string details)
    {
        var message = string.IsNullOrWhiteSpace(details)
            ? $"docker compose {command} failed with exit code {exitCode}."
            : details.Trim();
        return OperationError.Create("docker.compose.command.failed", message);
    }
}
