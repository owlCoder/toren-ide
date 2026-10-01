using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Errors;

namespace Toren.Workspaces.Adapters;

public sealed class DotNetSolutionProjectProvider(IProcessRunner processRunner) : ISolutionProjectProvider
{
    private readonly IProcessRunner _processRunner = processRunner
        ?? throw new ArgumentNullException(nameof(processRunner));

    public async Task<Result<IReadOnlyList<string>>> GetProjectPathsAsync(
        string solutionPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionPath);
        cancellationToken.ThrowIfCancellationRequested();

        var execution = await _processRunner.RunAsync(
            MsBuildEvaluationRequest.Create(solutionPath, "sln", solutionPath, "list"),
            cancellationToken).ConfigureAwait(false);

        if (!execution.IsSuccess)
        {
            return Result.Failure<IReadOnlyList<string>>(
                SolutionProjectErrors.ListFailed(execution.Error.Message));
        }

        if (!execution.Value.Succeeded)
        {
            return Result.Failure<IReadOnlyList<string>>(
                SolutionProjectErrors.ListFailed(ProcessFailureDetails.From(execution.Value)));
        }

        var solutionDirectory = Path.GetDirectoryName(solutionPath)
            ?? throw new InvalidOperationException("Solution has no directory.");
        var projects = execution.Value.StandardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.EndsWith("proj", StringComparison.OrdinalIgnoreCase))
            .Select(line => line.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar))
            .Select(line => Path.GetFullPath(Path.Combine(solutionDirectory, line)))
            .ToArray();

        return Result.Success<IReadOnlyList<string>>(projects);
    }
}
