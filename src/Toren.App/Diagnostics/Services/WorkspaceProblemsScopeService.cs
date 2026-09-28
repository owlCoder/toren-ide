using Toren.App.Diagnostics.Contracts;
using Toren.App.Diagnostics.Models;
using Toren.Core.Results;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Models;
using Toren.Workspaces.Services;

namespace Toren.App.Diagnostics.Services;

public sealed class WorkspaceProblemsScopeService(
    IWorkspaceClassifier workspaceClassifier,
    IWorkspaceProjectGraphService projectGraphService,
    IWorkspaceFileProvider workspaceFileProvider) : IProblemsWorkspaceScopeService
{
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private readonly IWorkspaceClassifier _workspaceClassifier = workspaceClassifier
        ?? throw new ArgumentNullException(nameof(workspaceClassifier));
    private readonly IWorkspaceProjectGraphService _projectGraphService = projectGraphService
        ?? throw new ArgumentNullException(nameof(projectGraphService));
    private readonly IWorkspaceFileProvider _workspaceFileProvider = workspaceFileProvider
        ?? throw new ArgumentNullException(nameof(workspaceFileProvider));

    public async Task<Result<ProblemsWorkspaceScopeIndex>> BuildAsync(
        string workspacePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        cancellationToken.ThrowIfCancellationRequested();

        var workspace = ClassifyWorkspace(workspacePath);
        if (workspace is null)
        {
            return Result.Success(ProblemsWorkspaceScopeIndex.Empty);
        }

        var graphResult = await _projectGraphService.LoadAsync(workspace, cancellationToken).ConfigureAwait(false);
        if (!graphResult.IsSuccess)
        {
            return Result.Failure<ProblemsWorkspaceScopeIndex>(graphResult.Error);
        }

        if (graphResult.Value.Projects.Count == 0)
        {
            return Result.Success(ProblemsWorkspaceScopeIndex.Empty);
        }

        var filesResult = await _workspaceFileProvider.GetFilesAsync(workspacePath, cancellationToken).ConfigureAwait(false);
        if (!filesResult.IsSuccess)
        {
            return Result.Failure<ProblemsWorkspaceScopeIndex>(filesResult.Error);
        }

        var projects = graphResult.Value.Projects;
        var ownership = new WorkspaceProjectOwnershipMap(projects);
        var filesByProject = projects.ToDictionary(
            project => Path.GetFullPath(project.Path),
            _ => new List<string>(),
            PathComparer);

        foreach (var file in filesResult.Value)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fullPath = Path.GetFullPath(file.Path);
            var project = ownership.FindOwningProject(fullPath);
            if (project is null)
            {
                continue;
            }

            filesByProject[Path.GetFullPath(project.Path)].Add(fullPath);
        }

        var scopes = projects
            .Select(project => new ProblemsProjectScope(
                Path.GetFullPath(project.Path),
                project.DisplayName,
                filesByProject[Path.GetFullPath(project.Path)]
                    .Distinct(PathComparer)
                    .OrderBy(static path => path, PathComparer)
                    .ToArray()))
            .ToArray();
        return Result.Success(new ProblemsWorkspaceScopeIndex(scopes));
    }

    private WorkspaceDescriptor? ClassifyWorkspace(string workspacePath)
    {
        if (Directory.Exists(workspacePath))
        {
            return _workspaceClassifier.ClassifyDirectory(workspacePath);
        }

        return _workspaceClassifier.TryClassifyFile(workspacePath, out var workspace)
            ? workspace
            : null;
    }
}
