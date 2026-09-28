using System.Text.Json;
using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Errors;

namespace Toren.Workspaces.Adapters;

public sealed class MsBuildProjectCompilationReferenceProvider(IProcessRunner processRunner)
    : IProjectCompilationReferenceProvider
{
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private readonly IProcessRunner _processRunner = processRunner
        ?? throw new ArgumentNullException(nameof(processRunner));

    public async Task<Result<IReadOnlyList<string>>> GetReferencePathsAsync(
        string projectPath,
        string? targetFramework = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        cancellationToken.ThrowIfCancellationRequested();

        var arguments = new List<string>
        {
            "msbuild",
            projectPath,
            "-nologo",
            "-verbosity:quiet",
            "-target:ResolveReferences",
            "-property:BuildProjectReferences=false",
        };
        if (!string.IsNullOrWhiteSpace(targetFramework))
        {
            arguments.Add($"-property:TargetFramework={targetFramework}");
        }

        arguments.Add("-getItem:ReferencePath");
        var execution = await _processRunner
            .RunAsync(ProcessRequest.Create("dotnet", arguments.ToArray()), cancellationToken)
            .ConfigureAwait(false);
        if (!execution.IsSuccess)
        {
            return Result.Failure<IReadOnlyList<string>>(
                ProjectCompilationReferenceErrors.ResolutionFailed(execution.Error.Message));
        }

        if (!execution.Value.Succeeded)
        {
            return Result.Failure<IReadOnlyList<string>>(
                ProjectCompilationReferenceErrors.ResolutionFailed(ProcessFailureDetails.From(execution.Value)));
        }

        try
        {
            using var document = JsonDocument.Parse(execution.Value.StandardOutput);
            if (!document.RootElement.TryGetProperty("Items", out var items)
                || items.ValueKind != JsonValueKind.Object
                || !items.TryGetProperty("ReferencePath", out var referencePaths)
                || referencePaths.ValueKind != JsonValueKind.Array)
            {
                return Result.Failure<IReadOnlyList<string>>(
                    ProjectCompilationReferenceErrors.ResolutionFailed(
                        "MSBuild output did not contain resolved ReferencePath items."));
            }

            var projectDirectory = Path.GetDirectoryName(Path.GetFullPath(projectPath))
                ?? Directory.GetCurrentDirectory();
            var paths = referencePaths
                .EnumerateArray()
                .Select(static item => GetProperty(item, "FullPath") ?? GetProperty(item, "Identity"))
                .Where(static path => !string.IsNullOrWhiteSpace(path))
                .Select(path => NormalizePath(path!, projectDirectory))
                .Distinct(PathComparer)
                .OrderBy(static path => path, PathComparer)
                .ToArray();
            return Result.Success<IReadOnlyList<string>>(paths);
        }
        catch (JsonException exception)
        {
            return Result.Failure<IReadOnlyList<string>>(
                ProjectCompilationReferenceErrors.ResolutionFailed(
                    $"MSBuild returned invalid reference data: {exception.Message}"));
        }
    }

    private static string NormalizePath(string path, string projectDirectory)
    {
        var normalizedPath = path
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.IsPathRooted(normalizedPath)
            ? normalizedPath
            : Path.Combine(projectDirectory, normalizedPath));
    }

    private static string? GetProperty(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var property))
        {
            return null;
        }

        var value = property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : property.ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
