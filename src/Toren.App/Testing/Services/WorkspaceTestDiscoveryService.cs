using Toren.App.Testing.Contracts;
using Toren.App.Testing.Models;
using Toren.Core.Results;
using Toren.DotNet.Testing.Contracts;
using Toren.DotNet.Testing.Models;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Models;

namespace Toren.App.Testing.Services;

public sealed class WorkspaceTestDiscoveryService(
    IWorkspaceProjectCatalog projectCatalog,
    IDotNetTestDiscoveryService testDiscoveryService) : IWorkspaceTestDiscoveryService
{
    private readonly IWorkspaceProjectCatalog _projectCatalog = projectCatalog
        ?? throw new ArgumentNullException(nameof(projectCatalog));
    private readonly IDotNetTestDiscoveryService _testDiscoveryService = testDiscoveryService
        ?? throw new ArgumentNullException(nameof(testDiscoveryService));

    public async Task<Result<IReadOnlyList<WorkspaceTestProjectDiscovery>>> DiscoverAsync(
        WorkspaceDescriptor workspace,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        var projects = await _projectCatalog
            .GetProjectsAsync(workspace, cancellationToken)
            .ConfigureAwait(false);
        if (!projects.IsSuccess)
        {
            return Result.Failure<IReadOnlyList<WorkspaceTestProjectDiscovery>>(projects.Error);
        }

        var discoveredProjects = new List<WorkspaceTestProjectDiscovery>();
        foreach (var project in projects.Value
                     .Where(static project => project.Metadata.IsTestProject)
                     .OrderBy(static project => project.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var targetFramework = project.Metadata.TargetFrameworks.Count == 1
                ? project.Metadata.TargetFrameworks[0]
                : null;
            var discovery = await _testDiscoveryService
                .DiscoverAsync(
                    new DotNetTestDiscoveryRequest(project.Path, TargetFramework: targetFramework),
                    cancellationToken)
                .ConfigureAwait(false);
            discoveredProjects.Add(discovery.IsSuccess
                ? new WorkspaceTestProjectDiscovery(
                    project.Path,
                    project.DisplayName,
                    discovery.Value)
                : new WorkspaceTestProjectDiscovery(
                    project.Path,
                    project.DisplayName,
                    [],
                    discovery.Error.Message));
        }

        return Result.Success<IReadOnlyList<WorkspaceTestProjectDiscovery>>(discoveredProjects);
    }
}
