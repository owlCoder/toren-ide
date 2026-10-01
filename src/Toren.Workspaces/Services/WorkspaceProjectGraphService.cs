using Toren.Core.Results;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Models;

namespace Toren.Workspaces.Services;

public sealed class WorkspaceProjectGraphService(
    IFolderProjectProvider folderProjectProvider,
    ISolutionProjectProvider solutionProjectProvider,
    IProjectMetadataProvider projectMetadataProvider,
    IProjectReferenceProvider projectReferenceProvider) : IWorkspaceProjectGraphService
{
    private readonly IFolderProjectProvider _folderProjectProvider = folderProjectProvider
        ?? throw new ArgumentNullException(nameof(folderProjectProvider));
    private readonly ISolutionProjectProvider _solutionProjectProvider = solutionProjectProvider
        ?? throw new ArgumentNullException(nameof(solutionProjectProvider));
    private readonly IProjectMetadataProvider _projectMetadataProvider = projectMetadataProvider
        ?? throw new ArgumentNullException(nameof(projectMetadataProvider));
    private readonly IProjectReferenceProvider _projectReferenceProvider = projectReferenceProvider
        ?? throw new ArgumentNullException(nameof(projectReferenceProvider));

    public async Task<Result<WorkspaceProjectGraph>> LoadAsync(
        WorkspaceDescriptor workspace,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        cancellationToken.ThrowIfCancellationRequested();

        var projectPaths = await GetProjectPathsAsync(workspace, cancellationToken).ConfigureAwait(false);
        if (!projectPaths.IsSuccess)
        {
            return Result.Failure<WorkspaceProjectGraph>(projectPaths.Error);
        }

        var displayNames = ProjectDisplayNameFormatter.Format(projectPaths.Value);
        var projects = new List<WorkspaceProject>(projectPaths.Value.Count);

        // Bound SDK processes so large solutions load concurrently without exhausting the host.
        var batchSize = Math.Clamp(Environment.ProcessorCount, 1, 4);
        for (var offset = 0; offset < projectPaths.Value.Count; offset += batchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = Math.Min(batchSize, projectPaths.Value.Count - offset);
            var evaluations = Enumerable.Range(offset, count).Select(LoadProjectAsync).ToArray();
            var results = await Task.WhenAll(evaluations).ConfigureAwait(false);
            foreach (var result in results)
            {
                if (!result.IsSuccess)
                {
                    return Result.Failure<WorkspaceProjectGraph>(result.Error);
                }

                projects.Add(result.Value);
            }
        }

        async Task<Result<WorkspaceProject>> LoadProjectAsync(int index)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var projectPath = projectPaths.Value[index];
            var metadata = await _projectMetadataProvider
                .GetMetadataAsync(projectPath, cancellationToken)
                .ConfigureAwait(false);
            if (!metadata.IsSuccess)
            {
                return Result.Failure<WorkspaceProject>(metadata.Error);
            }

            var references = await _projectReferenceProvider
                .GetReferencesAsync(projectPath, cancellationToken)
                .ConfigureAwait(false);
            if (!references.IsSuccess)
            {
                return Result.Failure<WorkspaceProject>(references.Error);
            }

            return Result.Success(new WorkspaceProject(
                projectPath, displayNames[index], metadata.Value, references.Value));
        }

        return Result.Success(new WorkspaceProjectGraph(projects));
    }

    private async Task<Result<IReadOnlyList<string>>> GetProjectPathsAsync(
        WorkspaceDescriptor workspace,
        CancellationToken cancellationToken)
    {
        return workspace.Kind switch
        {
            WorkspaceKind.Folder => await _folderProjectProvider
                .GetProjectPathsAsync(workspace.Path, cancellationToken)
                .ConfigureAwait(false),
            WorkspaceKind.Project => Result.Success<IReadOnlyList<string>>([Path.GetFullPath(workspace.Path)]),
            WorkspaceKind.Solution or WorkspaceKind.SolutionX => await _solutionProjectProvider
                .GetProjectPathsAsync(workspace.Path, cancellationToken)
                .ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(workspace)),
        };
    }
}
