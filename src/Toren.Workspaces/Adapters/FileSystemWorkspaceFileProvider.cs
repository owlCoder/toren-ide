using System.Collections.Frozen;
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

    public Task<Result<IReadOnlyList<WorkspaceFileEntry>>> GetFilesAsync(
        WorkspaceDescriptor workspace,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() => Discover(workspace, cancellationToken), cancellationToken);
    }

    private static Result<IReadOnlyList<WorkspaceFileEntry>> Discover(
        WorkspaceDescriptor workspace,
        CancellationToken cancellationToken)
    {
        var rootDirectory = GetRootDirectory(workspace);
        if (!Directory.Exists(rootDirectory))
        {
            return Result.Failure<IReadOnlyList<WorkspaceFileEntry>>(
                WorkspaceFileErrors.WorkspaceUnavailable(workspace.Path));
        }

        try
        {
            var files = new List<WorkspaceFileEntry>();
            var pending = new Stack<string>();
            pending.Push(rootDirectory);

            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var directory = pending.Pop();

                foreach (var filePath in Directory.EnumerateFiles(directory))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var fullPath = Path.GetFullPath(filePath);
                    files.Add(new WorkspaceFileEntry(
                        fullPath,
                        NormalizeRelativePath(Path.GetRelativePath(rootDirectory, fullPath)),
                        Path.GetFileName(fullPath)));
                }

                foreach (var childDirectory in Directory.EnumerateDirectories(directory))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (ShouldSkipDirectory(childDirectory))
                    {
                        continue;
                    }

                    pending.Push(childDirectory);
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

    private static string GetRootDirectory(WorkspaceDescriptor workspace)
    {
        var fullPath = Path.GetFullPath(workspace.Path);
        return workspace.Kind == WorkspaceKind.Folder
            ? fullPath
            : Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();
    }

    private static bool ShouldSkipDirectory(string directory)
    {
        if (ExcludedDirectories.Contains(Path.GetFileName(directory)))
        {
            return true;
        }

        return (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0;
    }

    private static string NormalizeRelativePath(string path) =>
        path.Replace(Path.DirectorySeparatorChar, '/');
}
