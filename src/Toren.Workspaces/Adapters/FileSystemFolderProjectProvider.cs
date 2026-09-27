using System.Collections.Frozen;
using System.Security;
using Toren.Core.Results;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Errors;

namespace Toren.Workspaces.Adapters;

public sealed class FileSystemFolderProjectProvider : IFolderProjectProvider
{
    private static readonly FrozenSet<string> ExcludedDirectories =
        new[] { ".git", ".idea", ".vs", "bin", "obj" }
            .ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public Task<Result<IReadOnlyList<string>>> GetProjectPathsAsync(
        string folderPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() => Discover(folderPath, cancellationToken), cancellationToken);
    }

    private static Result<IReadOnlyList<string>> Discover(
        string folderPath,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(folderPath))
        {
            return Result.Failure<IReadOnlyList<string>>(
                FolderProjectErrors.FolderUnavailable(folderPath));
        }

        try
        {
            var projects = new List<string>();
            var pending = new Stack<string>();
            pending.Push(Path.GetFullPath(folderPath));

            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var current = pending.Pop();

                foreach (var projectPath in Directory.EnumerateFiles(
                             current,
                             "*.csproj",
                             SearchOption.TopDirectoryOnly))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    projects.Add(Path.GetFullPath(projectPath));
                }

                foreach (var directory in Directory.EnumerateDirectories(current))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (ShouldSkipDirectory(directory))
                    {
                        continue;
                    }

                    pending.Push(directory);
                }
            }

            projects.Sort(StringComparer.OrdinalIgnoreCase);
            return Result.Success<IReadOnlyList<string>>(projects);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            return Result.Failure<IReadOnlyList<string>>(
                FolderProjectErrors.DiscoveryFailed(folderPath, exception.Message));
        }
    }

    private static bool ShouldSkipDirectory(string directory)
    {
        if (ExcludedDirectories.Contains(Path.GetFileName(directory)))
        {
            return true;
        }

        return (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0;
    }
}
