using Toren.App.Execution.Contracts;
using Toren.App.Execution.Models;
using Toren.Core.Results;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Models;

namespace Toren.App.Execution.Services;

public sealed class WorkspaceExecutionTargetService(IWorkspaceProjectCatalog projectCatalog)
    : IWorkspaceExecutionTargetService
{
    private readonly IWorkspaceProjectCatalog _projectCatalog = projectCatalog
        ?? throw new ArgumentNullException(nameof(projectCatalog));

    public async Task<Result<IReadOnlyList<WorkspaceExecutionTarget>>> GetTargetsAsync(
        WorkspaceDescriptor workspace,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        cancellationToken.ThrowIfCancellationRequested();

        // Runnable projects are known from evaluation alone; compiler inputs are not awaited.
        var projects = await _projectCatalog
            .GetProjectsAsync(workspace, cancellationToken)
            .ConfigureAwait(false);
        if (!projects.IsSuccess)
        {
            return Result.Failure<IReadOnlyList<WorkspaceExecutionTarget>>(projects.Error);
        }

        IReadOnlyList<WorkspaceExecutionTarget> targets = projects.Value
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
