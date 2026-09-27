using System.Collections.Frozen;
using System.Security;
using Toren.Core.Results;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Errors;
using Toren.Workspaces.Models;

namespace Toren.Workspaces.Services;

public sealed class WorkspaceTreeService(
    ISolutionProjectProvider solutionProjectProvider,
    IProjectReferenceProvider projectReferenceProvider) : IWorkspaceTreeService
{
    private static readonly FrozenSet<string> ExcludedDirectories =
        new[] { ".git", "bin", "obj" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private readonly ISolutionProjectProvider _solutionProjectProvider = solutionProjectProvider
        ?? throw new ArgumentNullException(nameof(solutionProjectProvider));
    private readonly IProjectReferenceProvider _projectReferenceProvider = projectReferenceProvider
        ?? throw new ArgumentNullException(nameof(projectReferenceProvider));

    public Result<WorkspaceNode> CreateRoot(WorkspaceDescriptor workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        var exists = workspace.Kind == WorkspaceKind.Folder
            ? Directory.Exists(workspace.Path)
            : File.Exists(workspace.Path);

        if (!exists)
        {
            return Result.Failure<WorkspaceNode>(WorkspaceTreeErrors.PathUnavailable(workspace.Path));
        }

        var kind = workspace.Kind switch
        {
            WorkspaceKind.Folder => WorkspaceNodeKind.Folder,
            WorkspaceKind.Project => WorkspaceNodeKind.Project,
            WorkspaceKind.Solution or WorkspaceKind.SolutionX => WorkspaceNodeKind.Solution,
            _ => throw new ArgumentOutOfRangeException(nameof(workspace)),
        };

        return Result.Success(new WorkspaceNode(workspace.Path, workspace.DisplayName, kind));
    }

    public async Task<Result<IReadOnlyList<WorkspaceNode>>> GetChildrenAsync(
        WorkspaceNode node,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        cancellationToken.ThrowIfCancellationRequested();

        return node.Kind switch
        {
            WorkspaceNodeKind.Folder => await Task.Run(
                () => ReadDirectory(node.Path, cancellationToken), cancellationToken).ConfigureAwait(false),
            WorkspaceNodeKind.Solution => await ReadSolutionAsync(node.Path, cancellationToken).ConfigureAwait(false),
            WorkspaceNodeKind.Project => await Task.Run(
                () => ReadProject(node.Path, cancellationToken), cancellationToken).ConfigureAwait(false),
            WorkspaceNodeKind.References => await ReadReferencesAsync(node.Path, cancellationToken).ConfigureAwait(false),
            _ => Result.Success<IReadOnlyList<WorkspaceNode>>([]),
        };
    }

    private static Result<IReadOnlyList<WorkspaceNode>> ReadDirectory(string path, CancellationToken cancellationToken)
    {
        try
        {
            var children = Directory.EnumerateFileSystemEntries(path)
                .Where(entry => !Directory.Exists(entry) || !ExcludedDirectories.Contains(Path.GetFileName(entry)))
                .Select(entry =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return CreateFileSystemNode(entry);
                })
                .OrderBy(node => node.Kind != WorkspaceNodeKind.Folder)
                .ThenBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return Result.Success<IReadOnlyList<WorkspaceNode>>(children);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            return Result.Failure<IReadOnlyList<WorkspaceNode>>(
                WorkspaceTreeErrors.ReadFailed(path, exception.Message));
        }
    }

    private static WorkspaceNode CreateFileSystemNode(string path)
    {
        var extension = Path.GetExtension(path);
        WorkspaceNodeKind kind;
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            kind = WorkspaceNodeKind.SymbolicLink;
        }
        else if (Directory.Exists(path))
        {
            kind = WorkspaceNodeKind.Folder;
        }
        else if (extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase))
        {
            kind = WorkspaceNodeKind.Solution;
        }
        else
        {
            kind = extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase)
                ? WorkspaceNodeKind.Project
                : WorkspaceNodeKind.File;
        }

        return new WorkspaceNode(path, Path.GetFileName(path), kind);
    }

    private async Task<Result<IReadOnlyList<WorkspaceNode>>> ReadSolutionAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return Result.Failure<IReadOnlyList<WorkspaceNode>>(WorkspaceTreeErrors.PathUnavailable(path));
        }

        var projectPaths = await _solutionProjectProvider
            .GetProjectPathsAsync(path, cancellationToken)
            .ConfigureAwait(false);
        if (!projectPaths.IsSuccess)
        {
            return Result.Failure<IReadOnlyList<WorkspaceNode>>(projectPaths.Error);
        }

        var displayNames = ProjectDisplayNameFormatter.Format(projectPaths.Value);
        var projects = projectPaths.Value
            .Select((projectPath, index) => new WorkspaceNode(
                projectPath,
                displayNames[index],
                WorkspaceNodeKind.Project))
            .ToArray();

        return Result.Success<IReadOnlyList<WorkspaceNode>>(projects);
    }

    private static Result<IReadOnlyList<WorkspaceNode>> ReadProject(
        string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return Result.Failure<IReadOnlyList<WorkspaceNode>>(WorkspaceTreeErrors.PathUnavailable(path));
        }

        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("Project has no directory.");
        var files = ReadDirectory(directory, cancellationToken);
        if (!files.IsSuccess)
        {
            return files;
        }

        var children = new List<WorkspaceNode>
        {
            new(path, "References", WorkspaceNodeKind.References),
        };
        children.AddRange(files.Value.Where(node =>
            node.Kind is not WorkspaceNodeKind.Project and not WorkspaceNodeKind.Solution));
        return Result.Success<IReadOnlyList<WorkspaceNode>>(children);
    }

    private async Task<Result<IReadOnlyList<WorkspaceNode>>> ReadReferencesAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return Result.Failure<IReadOnlyList<WorkspaceNode>>(WorkspaceTreeErrors.PathUnavailable(path));
        }

        var references = await _projectReferenceProvider
            .GetReferencesAsync(path, cancellationToken)
            .ConfigureAwait(false);
        if (!references.IsSuccess)
        {
            return Result.Failure<IReadOnlyList<WorkspaceNode>>(references.Error);
        }

        var nodes = references.Value
            .Select(reference => new WorkspaceNode(
                path,
                GetReferenceDisplayName(reference),
                WorkspaceNodeKind.Reference))
            .ToArray();
        return Result.Success<IReadOnlyList<WorkspaceNode>>(nodes);
    }

    private static string GetReferenceDisplayName(ProjectReferenceInfo reference)
    {
        if (reference.Kind != ProjectReferenceKind.Project)
        {
            return reference.Identity;
        }

        var source = reference.ResolvedPath ?? reference.Identity;
        var normalizedPath = source
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        var projectName = Path.GetFileNameWithoutExtension(normalizedPath);
        return string.IsNullOrWhiteSpace(projectName) ? reference.Identity : projectName;
    }
}
