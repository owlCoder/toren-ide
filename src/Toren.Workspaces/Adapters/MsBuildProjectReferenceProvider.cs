using System.Text.Json;
using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Errors;
using Toren.Workspaces.Models;

namespace Toren.Workspaces.Adapters;

public sealed class MsBuildProjectReferenceProvider(IProcessRunner processRunner) : IProjectReferenceProvider
{
    private readonly IProcessRunner _processRunner = processRunner
        ?? throw new ArgumentNullException(nameof(processRunner));

    public async Task<Result<IReadOnlyList<ProjectReferenceInfo>>> GetReferencesAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        cancellationToken.ThrowIfCancellationRequested();

        var execution = await _processRunner.RunAsync(
            ProcessRequest.Create(
                "dotnet",
                "msbuild",
                projectPath,
                "-nologo",
                "-verbosity:quiet",
                $"-getItem:{MsBuildEvaluationJson.ReferenceItems}"),
            cancellationToken).ConfigureAwait(false);

        if (!execution.IsSuccess)
        {
            return Result.Failure<IReadOnlyList<ProjectReferenceInfo>>(
                ProjectReferenceErrors.EvaluationFailed(execution.Error.Message));
        }

        if (!execution.Value.Succeeded)
        {
            return Result.Failure<IReadOnlyList<ProjectReferenceInfo>>(
                ProjectReferenceErrors.EvaluationFailed(ProcessFailureDetails.From(execution.Value)));
        }

        try
        {
            using var document = JsonDocument.Parse(execution.Value.StandardOutput);
            _ = MsBuildEvaluationJson.TryRead(document.RootElement, out var data, out var hasItems);
            if (!hasItems)
            {
                return Result.Failure<IReadOnlyList<ProjectReferenceInfo>>(
                    ProjectReferenceErrors.EvaluationFailed("MSBuild output did not contain evaluated items."));
            }

            return Result.Success(data.CreateReferences(MsBuildEvaluationData.GetProjectDirectory(projectPath)));
        }
        catch (JsonException exception)
        {
            return Result.Failure<IReadOnlyList<ProjectReferenceInfo>>(
                ProjectReferenceErrors.EvaluationFailed($"MSBuild returned invalid evaluation data: {exception.Message}"));
        }
    }
}
