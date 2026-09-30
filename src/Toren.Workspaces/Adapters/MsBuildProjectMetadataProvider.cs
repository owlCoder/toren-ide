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
        "TargetFramework,TargetFrameworks,OutputType,AssemblyName,RootNamespace,IsTestProject,ManagePackageVersionsCentrally,DirectoryBuildPropsPath,DirectoryBuildTargetsPath,DirectoryPackagesPropsPath,Nullable,LangVersion,DefineConstants,AllowUnsafeBlocks,GeneratedMSBuildEditorConfigFile,UsingMicrosoftNETSdkRazor";

    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

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
                $"-getProperty:{EvaluatedProperties}",
                "-getItem:Analyzer,Compile,Using,RazorComponent,RazorGenerate"),
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

            // Resolve compiler inputs without compiling the project. This includes SDK/package
            // generators, Razor inputs and framework preprocessor symbols.
            var arguments = new List<string>
            {
                "msbuild", projectPath, "-nologo", "-verbosity:quiet",
                "-target:PrepareForBuild,GenerateGlobalUsings,ResolveReferences,GenerateMSBuildEditorConfigFile",
                "-property:BuildProjectReferences=false",
                $"-getProperty:{EvaluatedProperties}", "-getItem:Analyzer,Compile,Using,AdditionalFiles,EditorConfigFiles",
            };
            var metadata = CreateMetadata(document.RootElement, properties, projectPath);
            if (GetProperty(properties, "TargetFramework") is null && metadata.TargetFrameworks.Count > 0)
            {
                arguments.Add($"-property:TargetFramework={metadata.TargetFrameworks[0]}");
            }

            var generated = await _processRunner.RunAsync(ProcessRequest.Create("dotnet", arguments.ToArray()),
                cancellationToken).ConfigureAwait(false);
            if (!generated.IsSuccess || !generated.Value.Succeeded)
            {
                // Keep the workspace browsable before restore; reference resolution separately
                // reports unavailable compiler inputs to the diagnostics pipeline.
                return Result.Success(metadata);
            }

            using var inputs = JsonDocument.Parse(generated.Value.StandardOutput);
            return Result.Success(CreateMetadata(inputs.RootElement, inputs.RootElement.GetProperty("Properties"), projectPath));
        }
        catch (JsonException exception)
        {
            return Result.Failure<ProjectMetadata>(
                ProjectMetadataErrors.EvaluationFailed(
                    $"MSBuild returned invalid evaluation data: {exception.Message}"));
        }
    }

    private static ProjectMetadata CreateMetadata(
        JsonElement root,
        JsonElement properties,
        string projectPath)
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
            GetProperty(properties, "DirectoryPackagesPropsPath"))
        {
            AnalyzerPaths = GetItemPaths(root, projectPath, "Analyzer"),
            SourcePaths = GetItemPaths(root, projectPath, "Compile"),
            GlobalUsings = GetGlobalUsings(root),
            DefineConstants = (GetProperty(properties, "DefineConstants") ?? string.Empty)
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            Nullable = GetProperty(properties, "Nullable"),
            LanguageVersion = GetProperty(properties, "LangVersion"),
            AllowUnsafe = GetBooleanProperty(properties, "AllowUnsafeBlocks"),
            AdditionalFilePaths = GetItemPaths(root, projectPath, "AdditionalFiles"),
            AnalyzerConfigPaths = GetItemPaths(root, projectPath, "EditorConfigFiles")
                .Concat(GetProperty(properties, "GeneratedMSBuildEditorConfigFile") is { } config
                    ? [NormalizeItemPath(config, Path.GetDirectoryName(Path.GetFullPath(projectPath))!)] : [])
                .Distinct(PathComparer).ToArray(),
        };
    }

    private static string[] GetItemPaths(JsonElement root, string projectPath, string itemName)
    {
        if (!root.TryGetProperty("Items", out var items)
            || items.ValueKind != JsonValueKind.Object
            || !items.TryGetProperty(itemName, out var analyzers)
            || analyzers.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var projectDirectory = Path.GetDirectoryName(Path.GetFullPath(projectPath))
            ?? Directory.GetCurrentDirectory();

        return analyzers
            .EnumerateArray()
            .Select(item => GetProperty(item, "FullPath") ?? GetProperty(item, "Identity"))
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(path => NormalizeItemPath(path!, projectDirectory))
            .Distinct(PathComparer)
            .OrderBy(static path => path, PathComparer)
            .ToArray();
    }

    private static string[] GetGlobalUsings(JsonElement root)
    {
        if (!root.TryGetProperty("Items", out var items)
            || !items.TryGetProperty("Using", out var usings)
            || usings.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return usings.EnumerateArray().Select(item =>
        {
            var name = GetProperty(item, "Identity");
            var alias = GetProperty(item, "Alias");
            var prefix = GetBooleanProperty(item, "Static") ? "static " : string.Empty;
            return string.IsNullOrWhiteSpace(name) ? null
                : $"global using {prefix}{(alias is null ? string.Empty : alias + " = ")}{name};";
        }).Where(static text => text is not null).Select(static text => text!).ToArray();
    }

    private static string NormalizeItemPath(string path, string projectDirectory) =>
        Path.GetFullPath(Path.IsPathRooted(path)
            ? path
            : Path.Combine(projectDirectory, path));

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
