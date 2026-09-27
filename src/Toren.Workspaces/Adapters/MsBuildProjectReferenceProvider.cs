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
    private const string EvaluatedReferenceItems = "ProjectReference,PackageReference,FrameworkReference";

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
                $"-getItem:{EvaluatedReferenceItems}"),
            cancellationToken).ConfigureAwait(false);

        if (!execution.IsSuccess)
        {
            return Result.Failure<IReadOnlyList<ProjectReferenceInfo>>(
                ProjectReferenceErrors.EvaluationFailed(execution.Error.Message));
        }

        if (!execution.Value.Succeeded)
        {
            return Result.Failure<IReadOnlyList<ProjectReferenceInfo>>(
                ProjectReferenceErrors.EvaluationFailed(GetProcessFailureDetails(execution.Value)));
        }

        try
        {
            using var document = JsonDocument.Parse(execution.Value.StandardOutput);
            if (!document.RootElement.TryGetProperty("Items", out var items))
            {
                return Result.Failure<IReadOnlyList<ProjectReferenceInfo>>(
                    ProjectReferenceErrors.EvaluationFailed("MSBuild output did not contain evaluated items."));
            }

            var references = new List<ProjectReferenceInfo>();
            AddReferences(references, items, "ProjectReference", ProjectReferenceKind.Project);
            AddReferences(references, items, "PackageReference", ProjectReferenceKind.Package);
            AddReferences(references, items, "FrameworkReference", ProjectReferenceKind.Framework);
            return Result.Success<IReadOnlyList<ProjectReferenceInfo>>(references);
        }
        catch (JsonException exception)
        {
            return Result.Failure<IReadOnlyList<ProjectReferenceInfo>>(
                ProjectReferenceErrors.EvaluationFailed($"MSBuild returned invalid evaluation data: {exception.Message}"));
        }
    }

    private static void AddReferences(
        List<ProjectReferenceInfo> destination,
        JsonElement items,
        string itemName,
        ProjectReferenceKind kind)
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
            if (!string.IsNullOrWhiteSpace(identity))
            {
                destination.Add(new ProjectReferenceInfo(identity, kind));
            }
        }
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
