using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.DotNet.Testing.Contracts;
using Toren.DotNet.Testing.Models;

namespace Toren.DotNet.Testing.Services;

public sealed class DotNetTestRunService(IStreamingProcessRunner processRunner) : IDotNetTestRunService
{
    private readonly IStreamingProcessRunner _processRunner = processRunner
        ?? throw new ArgumentNullException(nameof(processRunner));

    public Task<Result<ProcessResult>> RunAsync(
        DotNetTestRunRequest request,
        Action<ProcessOutputLine> onOutput,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ProjectPath);
        ArgumentNullException.ThrowIfNull(onOutput);

        var projectPath = Path.GetFullPath(request.ProjectPath);
        var runner = DotNetTestRunnerResolver.Resolve(projectPath);
        if (runner == DotNetTestRunner.MicrosoftTestingPlatform
            && !string.IsNullOrWhiteSpace(request.FullyQualifiedName)
            && string.IsNullOrWhiteSpace(request.RunnerId))
        {
            return Task.FromResult(Result.Failure<ProcessResult>(
                OperationError.Create(
                    "dotnet.test-run.mtp-selection-identity-required",
                    "Running one Microsoft Testing Platform test requires its discovered MTP test identity.")));
        }

        var arguments = new List<string> { "test" };
        if (runner == DotNetTestRunner.MicrosoftTestingPlatform)
        {
            arguments.Add("--project");
            arguments.Add(projectPath);
            arguments.Add("--no-ansi");
            arguments.Add("--no-progress");
        }
        else
        {
            arguments.Add(projectPath);
        }

        if (!string.IsNullOrWhiteSpace(request.Configuration))
        {
            arguments.Add("--configuration");
            arguments.Add(request.Configuration);
        }

        if (!string.IsNullOrWhiteSpace(request.TargetFramework))
        {
            arguments.Add("--framework");
            arguments.Add(request.TargetFramework);
        }

        if (runner == DotNetTestRunner.MicrosoftTestingPlatform
            && !string.IsNullOrWhiteSpace(request.RunnerId))
        {
            arguments.Add("--");
            arguments.Add("--filter-uid");
            arguments.Add(request.RunnerId);
        }
        else if (!string.IsNullOrWhiteSpace(request.FullyQualifiedName))
        {
            arguments.Add("--filter");
            arguments.Add($"FullyQualifiedName={request.FullyQualifiedName}");
        }

        return _processRunner.RunStreamingAsync(
            new ProcessRequest(
                "dotnet",
                arguments,
                Path.GetDirectoryName(projectPath)),
            onOutput,
            cancellationToken);
    }
}
