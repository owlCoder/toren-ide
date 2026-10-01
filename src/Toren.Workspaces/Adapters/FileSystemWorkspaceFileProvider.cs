using System.Collections.Frozen;
using System.IO.Enumeration;
using System.Security;
using Toren.Core.Results;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Errors;
using Toren.Workspaces.Models;

namespace Toren.Workspaces.Adapters;

public sealed class FileSystemWorkspaceFileProvider : IWorkspaceFileProvider
{
    private static readonly FrozenSet<string> ExcludedDirectories =
        new[] { ".git", ".idea", ".vs", "bin", "node_modules", "obj" }
            .ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    // Hidden and system entries are listed, and an unreadable directory is reported.
    private static readonly EnumerationOptions AllEntries = new()
    {
        AttributesToSkip = 0,
        IgnoreInaccessible = false,
        RecurseSubdirectories = false,
    };

    public Task<Result<IReadOnlyList<WorkspaceFileEntry>>> GetFilesAsync(
        string workspacePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() => Discover(workspacePath, cancellationToken), cancellationToken);
    }

    private static Result<IReadOnlyList<WorkspaceFileEntry>> Discover(
        string workspacePath,
        CancellationToken cancellationToken)
    {
        var rootDirectory = GetRootDirectory(workspacePath);
        if (!Directory.Exists(rootDirectory))
        {
            return Result.Failure<IReadOnlyList<WorkspaceFileEntry>>(
                WorkspaceFileErrors.WorkspaceUnavailable(workspacePath));
        }

        try
        {
            var files = new List<WorkspaceFileEntry>();
            var pending = new Stack<(string Directory, string RelativePrefix)>();
            pending.Push((rootDirectory, string.Empty));

            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var (directory, relativePrefix) = pending.Pop();

                // One pass per directory yields files and subdirectories together, and relative
                // paths are composed while descending instead of being recomputed per file.
                foreach (var entry in new FileSystemEnumerable<DirectoryEntry>(directory, ReadEntry, AllEntries))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!entry.IsDirectory)
                    {
                        files.Add(new WorkspaceFileEntry(
                            Path.Join(directory, entry.Name),
                            relativePrefix + entry.Name,
                            entry.Name));
                    }
                    else if (!ExcludedDirectories.Contains(entry.Name) && !entry.IsReparsePoint)
                    {
                        pending.Push((Path.Join(directory, entry.Name), $"{relativePrefix}{entry.Name}/"));
                    }
                }
            }

            files.Sort(static (left, right) =>
                StringComparer.OrdinalIgnoreCase.Compare(left.RelativePath, right.RelativePath));
            return Result.Success<IReadOnlyList<WorkspaceFileEntry>>(files);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            return Result.Failure<IReadOnlyList<WorkspaceFileEntry>>(
                WorkspaceFileErrors.DiscoveryFailed(rootDirectory, exception.Message));
        }
    }

    private static DirectoryEntry ReadEntry(ref FileSystemEntry entry) =>
        new(
            entry.FileName.ToString(),
            entry.IsDirectory,
            // Only directories are tested for links, so files cost no attribute lookup.
            entry.IsDirectory && (entry.Attributes & FileAttributes.ReparsePoint) != 0);

    private static string GetRootDirectory(string workspacePath)
    {
        var fullPath = Path.GetFullPath(workspacePath);
        if (Directory.Exists(fullPath))
        {
            return fullPath;
        }

        return File.Exists(fullPath)
            ? Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory()
            : fullPath;
    }

    private readonly record struct DirectoryEntry(string Name, bool IsDirectory, bool IsReparsePoint);
}
