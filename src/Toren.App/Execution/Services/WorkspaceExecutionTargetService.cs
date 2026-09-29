using Toren.App.Execution.Contracts;
using Toren.App.Execution.Models;
using Toren.Core.Results;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Models;

namespace Toren.App.Execution.Services;

public sealed class WorkspaceExecutionTargetService(IWorkspaceProjectGraphService projectGraphService)
    : IWorkspaceExecutionTargetService
{
    private readonly IWorkspaceProjectGraphService _projectGraphService = projectGraphService
        ?? throw new ArgumentNullException(nameof(projectGraphService));

    public async Task<Result<IReadOnlyList<WorkspaceExecutionTarget>>> GetTargetsAsync(
        WorkspaceDescriptor workspace,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        cancellationToken.ThrowIfCancellationRequested();

        var graphResult = await _projectGraphService
            .LoadAsync(workspace, cancellationToken)
            .ConfigureAwait(false);
        if (!graphResult.IsSuccess)
        {
            return Result.Failure<IReadOnlyList<WorkspaceExecutionTarget>>(graphResult.Error);
        }

        IReadOnlyList<WorkspaceExecutionTarget> targets = graphResult.Value.Projects
            .Where(static project => IsRunnable(project.Metadata))
            .Select(static project => new WorkspaceExecutionTarget(
                Path.GetFullPath(project.Path),
                project.DisplayName,
                project.Metadata.TargetFrameworks))
            .OrderBy(static target => target.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static target => target.ProjectPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return Result.Success(targets);
    }

    private static bool IsRunnable(ProjectMetadata metadata) =>
        !metadata.IsTestProject
        && (metadata.OutputType?.Equals("Exe", StringComparison.OrdinalIgnoreCase) == true
            || metadata.OutputType?.Equals("WinExe", StringComparison.OrdinalIgnoreCase) == true);
}
