using System.Security;
using System.Text;
using System.Xml.Linq;
using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.IO;
using Toren.Core.Results;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Errors;
using Toren.Workspaces.Models;

namespace Toren.Workspaces.Adapters;

/// <summary>
/// Evaluates all projects of a workspace in one <c>dotnet msbuild</c> invocation. MSBuild then
/// loads the SDK once and shares imported files and referenced-project evaluations between
/// projects, instead of repeating that work in a process per project. Projects the shared
/// invocation produced no result for are delegated to the per-project provider, which keeps
/// its failure semantics authoritative.
/// </summary>
public sealed class MsBuildBatchProjectEvaluationProvider : IProjectEvaluationProvider
{
    private const string EntryTarget = "Evaluate";

    private readonly IProcessRunner _processRunner;
    private readonly IProjectEvaluationProvider _perProjectProvider;
    private readonly int _maxNodes;

    /// <param name="processRunner">Runs the SDK process.</param>
    /// <param name="perProjectProvider">Evaluates projects the shared invocation could not.</param>
    /// <param name="maxNodes">
    /// MSBuild nodes of the invocation. Bounded, with node reuse disabled, so background
    /// evaluation neither exhausts the host nor leaves workers behind.
    /// </param>
    public MsBuildBatchProjectEvaluationProvider(
        IProcessRunner processRunner,
        IProjectEvaluationProvider perProjectProvider,
        int? maxNodes = null)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _perProjectProvider = perProjectProvider ?? throw new ArgumentNullException(nameof(perProjectProvider));
        _maxNodes = maxNodes ?? Math.Clamp(Environment.ProcessorCount, 1, 4);
        ArgumentOutOfRangeException.ThrowIfLessThan(_maxNodes, 1);
    }

    public async Task<Result<IReadOnlyList<ProjectEvaluation>>> EvaluateAsync(
        IReadOnlyList<string> projectPaths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projectPaths);
        cancellationToken.ThrowIfCancellationRequested();
        if (projectPaths.Count == 0)
        {
            return Result.Success<IReadOnlyList<ProjectEvaluation>>([]);
        }

        var batch = await RunAsync(
            MsBuildEvaluationTargets.EvaluationTarget,
            projectPaths.Select(static path => new BatchProject(path, TargetFramework: null)).ToArray(),
            compilerInputs: false,
            cancellationToken).ConfigureAwait(false);
        if (!batch.IsSuccess)
        {
            return Result.Failure<IReadOnlyList<ProjectEvaluation>>(
                ProjectMetadataErrors.EvaluationFailed(batch.Error.Message));
        }

        var evaluations = new ProjectEvaluation?[projectPaths.Count];
        var unresolved = new List<int>();
        for (var index = 0; index < evaluations.Length; index++)
        {
            if (batch.Value.Projects.TryGetValue(Path.GetFullPath(projectPaths[index]), out var data))
            {
                var projectDirectory = MsBuildEvaluationData.GetProjectDirectory(projectPaths[index]);
                evaluations[index] = new ProjectEvaluation(
                    data.CreateMetadata(projectDirectory),
                    data.CreateReferences(projectDirectory));
            }
            else
            {
                unresolved.Add(index);
            }
        }

        if (unresolved.Count > 0)
        {
            // Evaluated in order, so the first failure reported is the first failing project.
            var remaining = await _perProjectProvider
                .EvaluateAsync(unresolved.Select(index => projectPaths[index]).ToArray(), cancellationToken)
                .ConfigureAwait(false);
            if (!remaining.IsSuccess)
            {
                return remaining;
            }

            for (var position = 0; position < unresolved.Count; position++)
            {
                evaluations[unresolved[position]] = remaining.Value[position];
            }
        }

        return Result.Success<IReadOnlyList<ProjectEvaluation>>(evaluations!);
    }

    public async Task<Result<IReadOnlyList<ProjectMetadata>>> ResolveCompilerInputsAsync(
        IReadOnlyList<WorkspaceProject> projects,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projects);
        cancellationToken.ThrowIfCancellationRequested();
        if (projects.Count == 0)
        {
            return Result.Success<IReadOnlyList<ProjectMetadata>>([]);
        }

        var batch = await RunAsync(
            MsBuildEvaluationTargets.CompilerInputsTarget,
            projects.Select(static project => new BatchProject(
                project.Path,
                project.Metadata.TargetFrameworks.Count > 0 ? project.Metadata.TargetFrameworks[0] : null)).ToArray(),
            compilerInputs: true,
            cancellationToken).ConfigureAwait(false);

        var metadata = new ProjectMetadata?[projects.Count];
        var unresolved = new List<int>();
        for (var index = 0; index < metadata.Length; index++)
        {
            var project = projects[index];
            var projectPath = Path.GetFullPath(project.Path);
            if (!batch.IsSuccess)
            {
                // The SDK could not be started at all; a process per project would fare no better.
                metadata[index] = MsBuildProjectEvaluationProvider.WithoutCompilerInputs(
                    project.Metadata, batch.Error.Message);
            }
            else if (batch.Value.Projects.TryGetValue(projectPath, out var data))
            {
                metadata[index] = MsBuildProjectEvaluationProvider.CreateCompilerInputMetadata(
                    data, MsBuildEvaluationData.GetProjectDirectory(project.Path));
            }
            else if (batch.Value.Errors.TryGetValue(projectPath, out var errors))
            {
                // Keep the workspace browsable before restore; the error reaches the diagnostics
                // pipeline through the metadata instead of failing the whole graph.
                metadata[index] = MsBuildProjectEvaluationProvider.WithoutCompilerInputs(
                    project.Metadata, string.Join(Environment.NewLine, errors));
            }
            else
            {
                unresolved.Add(index);
            }
        }

        if (unresolved.Count > 0)
        {
            var remaining = await _perProjectProvider
                .ResolveCompilerInputsAsync(unresolved.Select(index => projects[index]).ToArray(), cancellationToken)
                .ConfigureAwait(false);
            if (!remaining.IsSuccess)
            {
                return remaining;
            }

            for (var position = 0; position < unresolved.Count; position++)
            {
                metadata[unresolved[position]] = remaining.Value[position];
            }
        }

        return Result.Success<IReadOnlyList<ProjectMetadata>>(metadata!);
    }

    private async Task<Result<BatchOutput>> RunAsync(
        string target,
        IReadOnlyList<BatchProject> projects,
        bool compilerInputs,
        CancellationToken cancellationToken)
    {
        try
        {
            return await RunInTemporaryDirectoryAsync(target, projects, compilerInputs, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            // The shared invocation needs temporary files and the per-project queries do not;
            // with no results here, every project is resolved individually.
            return Result.Success(new BatchOutput(
                new Dictionary<string, MsBuildEvaluationData>(FileSystemPath.Comparer),
                new Dictionary<string, List<string>>(FileSystemPath.Comparer)));
        }
    }

    private async Task<Result<BatchOutput>> RunInTemporaryDirectoryAsync(
        string target,
        IReadOnlyList<BatchProject> projects,
        bool compilerInputs,
        CancellationToken cancellationToken)
    {
        // A private directory per invocation: MSBuild imports the targets file written here, so
        // it must not be a location other users could write to.
        var directory = Directory.CreateTempSubdirectory("toren-evaluation-").FullName;
        try
        {
            var targetsPath = Path.Combine(directory, MsBuildEvaluationTargets.FileName);
            var traversalPath = Path.Combine(directory, "Evaluate.proj");
            var outputDirectory = Path.Combine(directory, "output") + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(outputDirectory);
            await File.WriteAllTextAsync(targetsPath, MsBuildEvaluationTargets.Text, cancellationToken)
                .ConfigureAwait(false);
            await File.WriteAllTextAsync(
                    traversalPath,
                    CreateTraversalProject(target, projects, targetsPath, outputDirectory, compilerInputs),
                    cancellationToken)
                .ConfigureAwait(false);

            var execution = await _processRunner.RunAsync(
                ProcessRequest.Create(
                    "dotnet",
                    "msbuild",
                    traversalPath,
                    "-nologo",
                    "-verbosity:quiet",
                    $"-target:{EntryTarget}",
                    $"-maxcpucount:{_maxNodes}",
                    "-nodeReuse:false"),
                cancellationToken).ConfigureAwait(false);
            if (!execution.IsSuccess)
            {
                return Result.Failure<BatchOutput>(execution.Error);
            }

            // The exit code is not consulted: a project that fails is simply absent from the
            // output and is resolved individually.
            var evaluated = new Dictionary<string, MsBuildEvaluationData>(FileSystemPath.Comparer);
            foreach (var file in Directory.EnumerateFiles(outputDirectory))
            {
                var lines = await File.ReadAllLinesAsync(file, cancellationToken).ConfigureAwait(false);
                if (MsBuildEvaluationText.Read(lines, out var data) is { } projectPath)
                {
                    evaluated.TryAdd(Path.GetFullPath(projectPath), data);
                }
            }

            return Result.Success(new BatchOutput(evaluated, ReadProjectErrors(execution.Value)));
        }
        finally
        {
            TryDelete(directory);
        }
    }

    private static string CreateTraversalProject(
        string target,
        IReadOnlyList<BatchProject> projects,
        string targetsPath,
        string outputDirectory,
        bool compilerInputs)
    {
        var properties = new List<string>
        {
            $"CustomBeforeMicrosoftCommonTargets={Escape(targetsPath)}",
            $"CustomBeforeMicrosoftCommonCrossTargetingTargets={Escape(targetsPath)}",
            $"_TorenOutputDirectory={Escape(outputDirectory)}",
        };
        if (compilerInputs)
        {
            properties.Add("BuildProjectReferences=false");
            properties.Add("_TorenCompilerInputs=true");
        }

        return new XElement(
            "Project",
            new XElement(
                "ItemGroup",
                projects.Select(static project => new XElement(
                    "TorenProject",
                    new XAttribute("Include", Escape(project.Path)),
                    project.TargetFramework is null
                        ? null
                        : new XAttribute("AdditionalProperties", $"TargetFramework={Escape(project.TargetFramework)}")))),
            new XElement(
                "Target",
                new XAttribute("Name", EntryTarget),
                new XElement(
                    "MSBuild",
                    new XAttribute("Projects", "@(TorenProject)"),
                    new XAttribute("Targets", target),
                    new XAttribute("Properties", string.Join(';', properties)),
                    new XAttribute("BuildInParallel", "true"),
                    // One project failing must not stop the evaluation of the others.
                    new XAttribute("ContinueOnError", "true")))).ToString();
    }

    /// <summary>
    /// Groups MSBuild diagnostics by the project named in their trailing
    /// <c>[path]</c> or <c>[path::TargetFramework=...]</c> suffix.
    /// </summary>
    private static Dictionary<string, List<string>> ReadProjectErrors(ProcessResult execution)
    {
        var errors = new Dictionary<string, List<string>>(FileSystemPath.Comparer);
        foreach (var output in new[] { execution.StandardOutput, execution.StandardError })
        {
            foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var suffixStart = line.LastIndexOf(" [", StringComparison.Ordinal);
                if (suffixStart < 0 || !line.EndsWith(']'))
                {
                    continue;
                }

                var project = line[(suffixStart + 2)..^1];
                var propertiesStart = project.IndexOf("::", StringComparison.Ordinal);
                if (propertiesStart >= 0)
                {
                    project = project[..propertiesStart];
                }

                if (!Path.IsPathFullyQualified(project))
                {
                    continue;
                }

                var projectPath = Path.GetFullPath(project);
                if (!errors.TryGetValue(projectPath, out var projectErrors))
                {
                    errors.Add(projectPath, projectErrors = []);
                }

                // Reported the way a build of that project alone would, without the property suffix.
                projectErrors.Add($"{line[..suffixStart]} [{project}]");
            }
        }

        return errors;
    }

    /// <summary>Escapes the characters MSBuild treats specially in item and property values.</summary>
    private static string Escape(string value)
    {
        var escaped = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (character is '%' or '*' or '?' or '@' or '$' or '(' or ')' or ';' or '\'')
            {
                escaped.Append('%').Append(((int)character).ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
            }
            else
            {
                escaped.Append(character);
            }
        }

        return escaped.ToString();
    }

    private static void TryDelete(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            // Left for the operating system's temporary-file cleanup.
        }
    }

    private sealed record BatchProject(string Path, string? TargetFramework);

    private sealed record BatchOutput(
        Dictionary<string, MsBuildEvaluationData> Projects,
        Dictionary<string, List<string>> Errors);
}
