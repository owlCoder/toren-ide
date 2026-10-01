using Toren.Core.Results;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Models;

namespace Toren.Workspaces.Services;

/// <summary>
/// Composes a project graph from provider contracts in two stages: evaluation, which is cheap
/// and enough for project lists, and design-time compiler inputs, which run targets. It does
/// not invoke external tools or scan the filesystem itself.
/// </summary>
public sealed class WorkspaceProjectGraphEvaluator(
    IFolderProjectProvider folderProjectProvider,
    ISolutionProjectProvider solutionProjectProvider,
    IProjectEvaluationProvider projectEvaluationProvider)
{
    private readonly IFolderProjectProvider _folderProjectProvider = folderProjectProvider
        ?? throw new ArgumentNullException(nameof(folderProjectProvider));
    private readonly ISolutionProjectProvider _solutionProjectProvider = solutionProjectProvider
        ?? throw new ArgumentNullException(nameof(solutionProjectProvider));
    private readonly IProjectEvaluationProvider _projectEvaluationProvider = projectEvaluationProvider
        ?? throw new ArgumentNullException(nameof(projectEvaluationProvider));

    public async Task<Result<WorkspaceProjectGraph>> EvaluateAsync(
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

        var evaluations = await _projectEvaluationProvider
            .EvaluateAsync(projectPaths.Value, cancellationToken)
            .ConfigureAwait(false);
        if (!evaluations.IsSuccess)
        {
            return Result.Failure<WorkspaceProjectGraph>(evaluations.Error);
        }

        var displayNames = ProjectDisplayNameFormatter.Format(projectPaths.Value);
        var projects = new WorkspaceProject[projectPaths.Value.Count];
        for (var index = 0; index < projects.Length; index++)
        {
            projects[index] = new WorkspaceProject(
                projectPaths.Value[index],
                displayNames[index],
                evaluations.Value[index].Metadata,
                evaluations.Value[index].References);
        }

        return Result.Success(new WorkspaceProjectGraph(projects));
    }

    public async Task<Result<WorkspaceProjectGraph>> ResolveCompilerInputsAsync(
        WorkspaceProjectGraph evaluated,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evaluated);
        cancellationToken.ThrowIfCancellationRequested();

        var metadata = await _projectEvaluationProvider
            .ResolveCompilerInputsAsync(evaluated.Projects, cancellationToken)
            .ConfigureAwait(false);
        if (!metadata.IsSuccess)
        {
            return Result.Failure<WorkspaceProjectGraph>(metadata.Error);
        }

        // Projects of one workspace largely resolve the same reference assemblies and analyzers,
        // and repeat the source paths already held by the evaluated graph. Keep one string each.
        var paths = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var project in evaluated.Projects)
        {
            SharePaths(project.Metadata, paths);
        }

        var projects = new WorkspaceProject[evaluated.Projects.Count];
        for (var index = 0; index < projects.Length; index++)
        {
            projects[index] = evaluated.Projects[index] with { Metadata = SharePaths(metadata.Value[index], paths) };
        }

        return Result.Success(new WorkspaceProjectGraph(projects));
    }

    private static ProjectMetadata SharePaths(ProjectMetadata metadata, Dictionary<string, string> paths)
    {
        return metadata with
        {
            SourcePaths = Share(metadata.SourcePaths),
            AnalyzerPaths = Share(metadata.AnalyzerPaths),
            AdditionalFilePaths = Share(metadata.AdditionalFilePaths),
            AnalyzerConfigPaths = Share(metadata.AnalyzerConfigPaths),
            ReferencePaths = metadata.ReferencePaths is null ? null : Share(metadata.ReferencePaths),
        };

        IReadOnlyList<string> Share(IReadOnlyList<string> values)
        {
            if (values.Count == 0)
            {
                return values;
            }

            var shared = new string[values.Count];
            for (var index = 0; index < shared.Length; index++)
            {
                if (!paths.TryGetValue(values[index], out var path))
                {
                    paths.Add(values[index], path = values[index]);
                }

                shared[index] = path;
            }

            return shared;
        }
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
