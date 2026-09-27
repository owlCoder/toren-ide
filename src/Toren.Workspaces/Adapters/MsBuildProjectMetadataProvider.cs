using System.Text.Json;
using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Errors;
using Toren.Workspaces.Models;

namespace Toren.Workspaces.Adapters;

public sealed class MsBuildProjectMetadataProvider(IProcessRunner processRunner) : IProjectMetadataProvider
{
    private const string EvaluatedProperties =
        "TargetFramework,TargetFrameworks,OutputType,AssemblyName,RootNamespace,IsTestProject,ManagePackageVersionsCentrally,DirectoryBuildPropsPath,DirectoryBuildTargetsPath,DirectoryPackagesPropsPath";

    private readonly IProcessRunner _processRunner = processRunner
        ?? throw new ArgumentNullException(nameof(processRunner));

    public async Task<Result<ProjectMetadata>> GetMetadataAsync(
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
                $"-getProperty:{EvaluatedProperties}"),
            cancellationToken).ConfigureAwait(false);

        if (!execution.IsSuccess)
        {
            return Result.Failure<ProjectMetadata>(
                ProjectMetadataErrors.EvaluationFailed(execution.Error.Message));
        }

        if (!execution.Value.Succeeded)
        {
            return Result.Failure<ProjectMetadata>(
                ProjectMetadataErrors.EvaluationFailed(ProcessFailureDetails.From(execution.Value)));
        }

        try
        {
            using var document = JsonDocument.Parse(execution.Value.StandardOutput);
            if (!document.RootElement.TryGetProperty("Properties", out var properties)
                || properties.ValueKind != JsonValueKind.Object)
            {
                return Result.Failure<ProjectMetadata>(
                    ProjectMetadataErrors.EvaluationFailed("MSBuild output did not contain evaluated properties."));
            }

            return Result.Success(CreateMetadata(properties));
        }
        catch (JsonException exception)
        {
            return Result.Failure<ProjectMetadata>(
                ProjectMetadataErrors.EvaluationFailed(
                    $"MSBuild returned invalid evaluation data: {exception.Message}"));
        }
    }

    private static ProjectMetadata CreateMetadata(JsonElement properties)
    {
        var targetFramework = GetProperty(properties, "TargetFramework");
        var targetFrameworks = GetProperty(properties, "TargetFrameworks");
        var frameworkValue = string.IsNullOrWhiteSpace(targetFrameworks)
            ? targetFramework
            : targetFrameworks;
        var frameworks = string.IsNullOrWhiteSpace(frameworkValue)
            ? []
            : frameworkValue
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

        return new ProjectMetadata(
            frameworks,
            GetProperty(properties, "OutputType"),
            GetProperty(properties, "AssemblyName"),
            GetProperty(properties, "RootNamespace"),
            GetBooleanProperty(properties, "IsTestProject"),
            GetBooleanProperty(properties, "ManagePackageVersionsCentrally"),
            GetProperty(properties, "DirectoryBuildPropsPath"),
            GetProperty(properties, "DirectoryBuildTargetsPath"),
            GetProperty(properties, "DirectoryPackagesPropsPath"));
    }

    private static string? GetProperty(JsonElement properties, string name)
    {
        if (!properties.TryGetProperty(name, out var property))
        {
            return null;
        }

        var value = property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : property.ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool GetBooleanProperty(JsonElement properties, string name) =>
        bool.TryParse(GetProperty(properties, name), out var value) && value;
}
