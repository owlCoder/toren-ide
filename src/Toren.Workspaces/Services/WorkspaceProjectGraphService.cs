using Toren.Core.Results;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Models;

namespace Toren.Workspaces.Services;

public sealed class WorkspaceProjectGraphService(
    ISolutionProjectProvider solutionProjectProvider,
    IProjectMetadataProvider projectMetadataProvider,
    IProjectReferenceProvider projectReferenceProvider) : IWorkspaceProjectGraphService
{
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

        for (var index = 0; index < projectPaths.Value.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var projectPath = projectPaths.Value[index];

            var metadata = await _projectMetadataProvider
                .GetMetadataAsync(projectPath, cancellationToken)
                .ConfigureAwait(false);
            if (!metadata.IsSuccess)
            {
                return Result.Failure<WorkspaceProjectGraph>(metadata.Error);
            }

            var references = await _projectReferenceProvider
                .GetReferencesAsync(projectPath, cancellationToken)
                .ConfigureAwait(false);
            if (!references.IsSuccess)
            {
                return Result.Failure<WorkspaceProjectGraph>(references.Error);
            }

            projects.Add(new WorkspaceProject(
                projectPath,
                displayNames[index],
                metadata.Value,
                references.Value));
        }

        return Result.Success(new WorkspaceProjectGraph(projects));
    }

    private async Task<Result<IReadOnlyList<string>>> GetProjectPathsAsync(
        WorkspaceDescriptor workspace,
        CancellationToken cancellationToken)
    {
        return workspace.Kind switch
        {
            WorkspaceKind.Folder => Result.Success<IReadOnlyList<string>>([]),
            WorkspaceKind.Project => Result.Success<IReadOnlyList<string>>([Path.GetFullPath(workspace.Path)]),
            WorkspaceKind.Solution or WorkspaceKind.SolutionX => await _solutionProjectProvider
                .GetProjectPathsAsync(workspace.Path, cancellationToken)
                .ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(workspace)),
        };
    }
}
