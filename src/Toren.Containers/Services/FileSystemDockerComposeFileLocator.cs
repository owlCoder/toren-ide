using Toren.Containers.Contracts;
using Toren.Core.Results;

namespace Toren.Containers.Services;

public sealed class FileSystemDockerComposeFileLocator : IDockerComposeFileLocator
{
    private static readonly string[] CandidateFileNames =
    [
        "compose.yaml",
        "compose.yml",
        "docker-compose.yaml",
        "docker-compose.yml",
    ];

    public Result<string?> Find(string workspacePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        var fullPath = Path.GetFullPath(workspacePath);
        var directory = Directory.Exists(fullPath)
            ? fullPath
            : File.Exists(fullPath)
                ? Path.GetDirectoryName(fullPath)
                : null;
        if (string.IsNullOrWhiteSpace(directory))
        {
            return Result.Success<string?>(null);
        }

        foreach (var candidate in CandidateFileNames)
        {
            var path = Path.Combine(directory, candidate);
            if (File.Exists(path))
            {
                return Result.Success<string?>(path);
            }
        }

        return Result.Success<string?>(null);
    }
}
