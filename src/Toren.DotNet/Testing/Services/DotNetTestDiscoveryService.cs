using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.DotNet.Testing.Contracts;
using Toren.DotNet.Testing.Models;

namespace Toren.DotNet.Testing.Services;

public sealed class DotNetTestDiscoveryService(IProcessRunner processRunner) : IDotNetTestDiscoveryService
{
    private readonly IProcessRunner _processRunner = processRunner
        ?? throw new ArgumentNullException(nameof(processRunner));

    public async Task<Result<IReadOnlyList<DotNetTestCase>>> DiscoverAsync(
        DotNetTestDiscoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ProjectPath);

        var projectPath = Path.GetFullPath(request.ProjectPath);
        var arguments = new List<string>
        {
            "test",
            projectPath,
            "--list-tests",
            "--nologo",
            "--verbosity",
            "quiet",
        };
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

        var result = await _processRunner.RunAsync(
            new ProcessRequest(
                "dotnet",
                arguments,
                Path.GetDirectoryName(projectPath)),
            cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return Result.Failure<IReadOnlyList<DotNetTestCase>>(result.Error);
        }

        if (!result.Value.Succeeded)
        {
            var message = string.IsNullOrWhiteSpace(result.Value.StandardError)
                ? $"Test discovery failed with exit code {result.Value.ExitCode}."
                : result.Value.StandardError.Trim();
            return Result.Failure<IReadOnlyList<DotNetTestCase>>(
                OperationError.Create("dotnet.test-discovery.failed", message));
        }

        return Result.Success<IReadOnlyList<DotNetTestCase>>(
            DotNetTestListParser.Parse(result.Value.StandardOutput));
    }
}
