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
                ProjectReferenceErrors.EvaluationFailed(ProcessFailureDetails.From(execution.Value)));
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
            AddReferences(references, items, "ProjectReference", ProjectReferenceKind.Project, projectPath);
            AddReferences(references, items, "PackageReference", ProjectReferenceKind.Package, projectPath);
            AddReferences(references, items, "FrameworkReference", ProjectReferenceKind.Framework, projectPath);
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
        ProjectReferenceKind kind,
        string projectPath)
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

            var resolvedPath = kind == ProjectReferenceKind.Project
                ? ResolveProjectReferencePath(projectPath, identity)
                : null;
            destination.Add(new ProjectReferenceInfo(identity, kind, resolvedPath));
        }
    }

    private static string ResolveProjectReferencePath(string projectPath, string identity)
    {
        var projectDirectory = Path.GetDirectoryName(Path.GetFullPath(projectPath))
            ?? throw new InvalidOperationException("Project has no directory.");
        var normalizedIdentity = identity
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        var referencePath = Path.IsPathRooted(normalizedIdentity)
            ? normalizedIdentity
            : Path.Combine(projectDirectory, normalizedIdentity);
        return Path.GetFullPath(referencePath);
    }
}
