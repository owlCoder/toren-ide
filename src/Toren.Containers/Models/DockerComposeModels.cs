namespace Toren.Containers.Models;

public sealed record DockerComposeRequest(
    string ComposeFilePath,
    string? ProjectName = null,
    bool Detached = true);

public sealed record DockerComposeLogsRequest(
    string ComposeFilePath,
    string? ProjectName = null,
    int Tail = 200);

public sealed record DockerComposeToolStatus(
    bool IsAvailable,
    string? Version,
    string? Details);
