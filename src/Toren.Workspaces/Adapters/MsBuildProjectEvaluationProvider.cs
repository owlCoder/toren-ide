using System.Text.Json;
using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Errors;
using Toren.Workspaces.Models;

namespace Toren.Workspaces.Adapters;

/// <summary>
/// Evaluates each project with its own <c>dotnet msbuild</c> process. This is the reference
/// behavior; <see cref="MsBuildBatchProjectEvaluationProvider"/> uses it for any project that
/// a shared invocation could not handle.
/// </summary>
public sealed class MsBuildProjectEvaluationProvider : IProjectEvaluationProvider
{
    private readonly IProcessRunner _processRunner;
    private readonly int _maxConcurrency;

    /// <param name="processRunner">Runs the SDK processes.</param>
    /// <param name="maxConcurrency">
    /// How many projects are evaluated at once. SDK processes are bounded so large solutions
    /// load concurrently without exhausting the host.
    /// </param>
    public MsBuildProjectEvaluationProvider(IProcessRunner processRunner, int? maxConcurrency = null)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _maxConcurrency = maxConcurrency ?? Math.Clamp(Environment.ProcessorCount, 1, 4);
        ArgumentOutOfRangeException.ThrowIfLessThan(_maxConcurrency, 1);
    }

    public Task<Result<IReadOnlyList<ProjectEvaluation>>> EvaluateAsync(
        IReadOnlyList<string> projectPaths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projectPaths);
        cancellationToken.ThrowIfCancellationRequested();
        return RunBoundedAsync(projectPaths, EvaluateProjectAsync, cancellationToken);
    }

    public Task<Result<IReadOnlyList<ProjectMetadata>>> ResolveCompilerInputsAsync(
        IReadOnlyList<WorkspaceProject> projects,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projects);
        cancellationToken.ThrowIfCancellationRequested();
        return RunBoundedAsync(projects, ResolveProjectCompilerInputsAsync, cancellationToken);
    }

    internal static ProjectMetadata WithoutCompilerInputs(ProjectMetadata evaluated, string details) =>
        evaluated with
        {
            ReferencePaths = null,
            CompilerInputsError = ProjectCompilationReferenceErrors.ResolutionFailed(details),
        };

    /// <summary>
    /// Adds the resolved reference assemblies to design-time metadata, or the reason they are
    /// missing when MSBuild did not report the item group.
    /// </summary>
    internal static ProjectMetadata CreateCompilerInputMetadata(MsBuildEvaluationData data, string projectDirectory)
    {
        var metadata = data.CreateMetadata(projectDirectory);
        return data.HasItems(MsBuildEvaluationData.ReferencePathItem)
            ? metadata with
            {
                ReferencePaths = data.GetOrderedPaths(MsBuildEvaluationData.ReferencePathItem, projectDirectory),
            }
            : metadata with
            {
                CompilerInputsError = ProjectCompilationReferenceErrors.ResolutionFailed(
                    "MSBuild output did not contain resolved ReferencePath items."),
            };
    }

    private async Task<Result<ProjectEvaluation>> EvaluateProjectAsync(
        string projectPath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);

        // One evaluation serves both the metadata and the declared references of a project.
        var execution = await _processRunner.RunAsync(
            ProcessRequest.Create(
                "dotnet",
                "msbuild",
                projectPath,
                "-nologo",
                "-verbosity:quiet",
                $"-getProperty:{MsBuildEvaluationJson.EvaluatedProperties}",
                $"-getItem:Analyzer,Compile,Using,RazorComponent,RazorGenerate,{MsBuildEvaluationJson.ReferenceItems}"),
            cancellationToken).ConfigureAwait(false);

        if (!execution.IsSuccess)
        {
            return Result.Failure<ProjectEvaluation>(
                ProjectMetadataErrors.EvaluationFailed(execution.Error.Message));
        }

        if (!execution.Value.Succeeded)
        {
            return Result.Failure<ProjectEvaluation>(
                ProjectMetadataErrors.EvaluationFailed(ProcessFailureDetails.From(execution.Value)));
        }

        try
        {
            using var document = JsonDocument.Parse(execution.Value.StandardOutput);
            if (!MsBuildEvaluationJson.TryRead(document.RootElement, out var data, out _))
            {
                return Result.Failure<ProjectEvaluation>(MissingProperties());
            }

            var projectDirectory = MsBuildEvaluationData.GetProjectDirectory(projectPath);
            return Result.Success(new ProjectEvaluation(
                data.CreateMetadata(projectDirectory),
                data.CreateReferences(projectDirectory)));
        }
        catch (JsonException exception)
        {
            return Result.Failure<ProjectEvaluation>(InvalidEvaluationData(exception));
        }
    }

    private async Task<Result<ProjectMetadata>> ResolveProjectCompilerInputsAsync(
        WorkspaceProject project,
        CancellationToken cancellationToken)
    {
        // Resolve compiler inputs without compiling the project. This includes SDK/package
        // generators, Razor inputs, framework preprocessor symbols and reference assemblies.
        var arguments = new List<string>
        {
            "msbuild", project.Path, "-nologo", "-verbosity:quiet",
            $"-target:{string.Join(',', MsBuildEvaluationTargets.CompilerInputTargets)}",
            "-property:BuildProjectReferences=false",
        };
        if (project.Metadata.TargetFrameworks.Count > 0)
        {
            arguments.Add($"-property:TargetFramework={project.Metadata.TargetFrameworks[0]}");
        }

        arguments.Add($"-getProperty:{MsBuildEvaluationJson.EvaluatedProperties}");
        arguments.Add("-getItem:Analyzer,Compile,Using,AdditionalFiles,EditorConfigFiles,ReferencePath");

        var execution = await _processRunner
            .RunAsync(ProcessRequest.Create("dotnet", arguments.ToArray()), cancellationToken)
            .ConfigureAwait(false);
        if (!execution.IsSuccess)
        {
            return Result.Success(WithoutCompilerInputs(project.Metadata, execution.Error.Message));
        }

        if (!execution.Value.Succeeded)
        {
            // Keep the workspace browsable before restore; the error reaches the diagnostics
            // pipeline through the metadata instead of failing the whole graph.
            return Result.Success(WithoutCompilerInputs(project.Metadata, ProcessFailureDetails.From(execution.Value)));
        }

        try
        {
            using var document = JsonDocument.Parse(execution.Value.StandardOutput);
            return MsBuildEvaluationJson.TryRead(document.RootElement, out var data, out _)
                ? Result.Success(CreateCompilerInputMetadata(
                    data,
                    MsBuildEvaluationData.GetProjectDirectory(project.Path)))
                : Result.Failure<ProjectMetadata>(MissingProperties());
        }
        catch (JsonException exception)
        {
            return Result.Failure<ProjectMetadata>(InvalidEvaluationData(exception));
        }
    }

    private static OperationError MissingProperties() =>
        ProjectMetadataErrors.EvaluationFailed("MSBuild output did not contain evaluated properties.");

    private static OperationError InvalidEvaluationData(JsonException exception) =>
        ProjectMetadataErrors.EvaluationFailed($"MSBuild returned invalid evaluation data: {exception.Message}");

    /// <summary>
    /// Runs one operation per input with bounded overlap and returns results in input order.
    /// Inputs are claimed in order and claiming stops at the first failure, so the reported
    /// error is always that of the first failing project.
    /// </summary>
    private async Task<Result<IReadOnlyList<TResult>>> RunBoundedAsync<TInput, TResult>(
        IReadOnlyList<TInput> inputs,
        Func<TInput, CancellationToken, Task<Result<TResult>>> operation,
        CancellationToken cancellationToken)
    {
        var results = new Result<TResult>?[inputs.Count];
        var nextIndex = -1;
        var firstFailedIndex = int.MaxValue;

        async Task RunWorkerAsync()
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var index = Interlocked.Increment(ref nextIndex);
                if (index >= inputs.Count || index > Volatile.Read(ref firstFailedIndex))
                {
                    return;
                }

                Result<TResult> result;
                try
                {
                    result = await operation(inputs[index], cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // Cancellation or an unexpected fault: stop the other workers from claiming more.
                    Volatile.Write(ref firstFailedIndex, -1);
                    throw;
                }

                results[index] = result;
                if (result.IsFailure)
                {
                    int observed;
                    do
                    {
                        observed = Volatile.Read(ref firstFailedIndex);
                    }
                    while (index < observed
                        && Interlocked.CompareExchange(ref firstFailedIndex, index, observed) != observed);
                }
            }
        }

        var workers = new Task[Math.Min(_maxConcurrency, inputs.Count)];
        for (var worker = 0; worker < workers.Length; worker++)
        {
            workers[worker] = RunWorkerAsync();
        }

        // WhenAll completes only after every worker has stopped, so started operations are
        // always drained before a failure or cancellation propagates.
        await Task.WhenAll(workers).ConfigureAwait(false);

        if (firstFailedIndex != int.MaxValue)
        {
            return Result.Failure<IReadOnlyList<TResult>>(results[firstFailedIndex]!.Error);
        }

        var values = new TResult[inputs.Count];
        for (var index = 0; index < values.Length; index++)
        {
            values[index] = results[index]!.Value!;
        }

        return Result.Success<IReadOnlyList<TResult>>(values);
    }
}
