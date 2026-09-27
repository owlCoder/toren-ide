using System.Collections.Frozen;
using System.Security;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Errors;
using Toren.Workspaces.Models;

namespace Toren.Workspaces.Services;

public sealed class WorkspaceTreeService(IProcessRunner processRunner) : IWorkspaceTreeService
{
    private const string EvaluatedReferenceItems = "ProjectReference,PackageReference,FrameworkReference";

    private static readonly FrozenSet<string> ExcludedDirectories =
        new[] { ".git", "bin", "obj" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private readonly IProcessRunner _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));

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
            WorkspaceNodeKind.References => await ReadEvaluatedReferencesAsync(node.Path, cancellationToken).ConfigureAwait(false),
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

        var execution = await _processRunner.RunAsync(
            ProcessRequest.Create("dotnet", "sln", path, "list"),
            cancellationToken).ConfigureAwait(false);

        if (!execution.IsSuccess)
        {
            return Result.Failure<IReadOnlyList<WorkspaceNode>>(
                WorkspaceTreeErrors.SolutionListFailed(execution.Error.Message));
        }

        if (!execution.Value.Succeeded)
        {
            var details = GetProcessFailureDetails(execution.Value);
            return Result.Failure<IReadOnlyList<WorkspaceNode>>(
                WorkspaceTreeErrors.SolutionListFailed(details));
        }

        var solutionDirectory = Path.GetDirectoryName(path) ?? throw new InvalidOperationException("Solution has no directory.");
        var projects = execution.Value.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.EndsWith("proj", StringComparison.OrdinalIgnoreCase))
            .Select(line => line.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar))
            .Select(line => Path.GetFullPath(Path.Combine(solutionDirectory, line)))
            .Select(projectPath => new WorkspaceNode(
                projectPath,
                Path.GetFileNameWithoutExtension(projectPath),
                WorkspaceNodeKind.Project))
            .ToArray();

        return Result.Success<IReadOnlyList<WorkspaceNode>>(projects);
    }

    private static Result<IReadOnlyList<WorkspaceNode>> ReadProject(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return Result.Failure<IReadOnlyList<WorkspaceNode>>(WorkspaceTreeErrors.PathUnavailable(path));
        }

        var directory = Path.GetDirectoryName(path) ?? throw new InvalidOperationException("Project has no directory.");
        var files = ReadDirectory(directory, cancellationToken);
        if (!files.IsSuccess)
        {
            return files;
        }

        var references = ReadDeclaredReferences(path, cancellationToken);
        if (!references.IsSuccess)
        {
            return references;
        }

        var children = new List<WorkspaceNode>();
        if (references.Value.Count > 0)
        {
            children.Add(new WorkspaceNode(path, "References", WorkspaceNodeKind.References));
        }

        children.AddRange(files.Value.Where(node =>
            node.Kind is not WorkspaceNodeKind.Project and not WorkspaceNodeKind.Solution));
        return Result.Success<IReadOnlyList<WorkspaceNode>>(children);
    }

    private async Task<Result<IReadOnlyList<WorkspaceNode>>> ReadEvaluatedReferencesAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return Result.Failure<IReadOnlyList<WorkspaceNode>>(WorkspaceTreeErrors.PathUnavailable(path));
        }

        var execution = await _processRunner.RunAsync(
            ProcessRequest.Create(
                "dotnet",
                "msbuild",
                path,
                "-nologo",
                "-verbosity:quiet",
                $"-getItem:{EvaluatedReferenceItems}"),
            cancellationToken).ConfigureAwait(false);

        if (!execution.IsSuccess)
        {
            return Result.Failure<IReadOnlyList<WorkspaceNode>>(
                WorkspaceTreeErrors.ProjectEvaluationFailed(execution.Error.Message));
        }

        if (!execution.Value.Succeeded)
        {
            return Result.Failure<IReadOnlyList<WorkspaceNode>>(
                WorkspaceTreeErrors.ProjectEvaluationFailed(GetProcessFailureDetails(execution.Value)));
        }

        try
        {
            using var document = JsonDocument.Parse(execution.Value.StandardOutput);
            if (!document.RootElement.TryGetProperty("Items", out var items))
            {
                return Result.Failure<IReadOnlyList<WorkspaceNode>>(
                    WorkspaceTreeErrors.ProjectEvaluationFailed("MSBuild output did not contain evaluated items."));
            }

            var references = new List<WorkspaceNode>();
            AddEvaluatedReferences(references, path, items, "ProjectReference");
            AddEvaluatedReferences(references, path, items, "PackageReference");
            AddEvaluatedReferences(references, path, items, "FrameworkReference");
            return Result.Success<IReadOnlyList<WorkspaceNode>>(references);
        }
        catch (JsonException exception)
        {
            return Result.Failure<IReadOnlyList<WorkspaceNode>>(
                WorkspaceTreeErrors.ProjectEvaluationFailed($"MSBuild returned invalid evaluation data: {exception.Message}"));
        }
    }

    private static void AddEvaluatedReferences(
        List<WorkspaceNode> destination,
        string projectPath,
        JsonElement items,
        string itemName)
    {
        if (!items.TryGetProperty(itemName, out var itemGroup) || itemGroup.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in itemGroup.EnumerateArray())
        {
            if (!item.TryGetProperty("Identity", out var identityElement))
            {
                continue;
            }

            var identity = identityElement.GetString();
            if (string.IsNullOrWhiteSpace(identity))
            {
                continue;
            }

            destination.Add(new WorkspaceNode(
                projectPath,
                GetReferenceDisplayName(itemName, identity),
                WorkspaceNodeKind.Reference));
        }
    }

    private static Result<IReadOnlyList<WorkspaceNode>> ReadDeclaredReferences(
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            var document = XDocument.Load(path);
            cancellationToken.ThrowIfCancellationRequested();
            var references = document.Descendants()
                .Where(element => element.Name.LocalName is "ProjectReference" or "PackageReference" or "FrameworkReference")
                .Select(element => new
                {
                    Kind = element.Name.LocalName,
                    Include = (string?)element.Attribute("Include"),
                })
                .Where(reference => !string.IsNullOrWhiteSpace(reference.Include))
                .Select(reference => new WorkspaceNode(
                    path,
                    GetReferenceDisplayName(reference.Kind, reference.Include!),
                    WorkspaceNodeKind.Reference))
                .ToArray();

            return Result.Success<IReadOnlyList<WorkspaceNode>>(references);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException or XmlException)
        {
            return Result.Failure<IReadOnlyList<WorkspaceNode>>(
                WorkspaceTreeErrors.ReadFailed(path, exception.Message));
        }
    }

    private static string GetReferenceDisplayName(string kind, string include)
    {
        if (!kind.Equals("ProjectReference", StringComparison.Ordinal))
        {
            return include;
        }

        var normalizedPath = include
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        var projectName = Path.GetFileNameWithoutExtension(normalizedPath);
        return string.IsNullOrWhiteSpace(projectName) ? include : projectName;
    }

    private static string GetProcessFailureDetails(ProcessResult processResult)
    {
        var details = string.IsNullOrWhiteSpace(processResult.StandardError)
            ? processResult.StandardOutput.Trim()
            : processResult.StandardError.Trim();
        return string.IsNullOrWhiteSpace(details)
            ? "The .NET CLI did not provide error details."
            : details;
    }
}
