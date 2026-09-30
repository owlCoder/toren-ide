using System.Globalization;
using Toren.Containers.Contracts;
using Toren.Containers.Errors;
using Toren.Containers.Models;
using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;

namespace Toren.Containers.Services;

public sealed class DockerComposeService(IProcessRunner processRunner) : IDockerComposeService
{
    private readonly IProcessRunner _processRunner = processRunner
        ?? throw new ArgumentNullException(nameof(processRunner));

    public async Task<Result<DockerComposeToolStatus>> DetectAsync(
        CancellationToken cancellationToken = default)
    {
        var execution = await RunAsync(
            ["compose", "version", "--short"],
            Directory.GetCurrentDirectory(),
            cancellationToken).ConfigureAwait(false);
        if (execution.IsFailure)
        {
            return Result.Failure<DockerComposeToolStatus>(execution.Error);
        }

        var result = execution.Value!;
        return Result.Success(
            new DockerComposeToolStatus(
                result.Succeeded,
                result.Succeeded ? result.StandardOutput.Trim() : null,
                result.Succeeded ? null : NormalizeDetails(result)));
    }

    public Task<Result<bool>> UpAsync(
        DockerComposeRequest request,
        CancellationToken cancellationToken = default)
    {
        var resolved = ResolveRequest(request);
        if (resolved.IsFailure)
        {
            return Task.FromResult(Result.Failure<bool>(resolved.Error));
        }

        var arguments = CreateBaseArguments(resolved.Value!);
        arguments.Add("up");
        if (resolved.Value!.Detached)
        {
            arguments.Add("--detach");
        }

        return RunMutationAsync("up", arguments, resolved.Value.ComposeFilePath, cancellationToken);
    }

    public Task<Result<bool>> DownAsync(
        DockerComposeRequest request,
        CancellationToken cancellationToken = default)
    {
        var resolved = ResolveRequest(request);
        if (resolved.IsFailure)
        {
            return Task.FromResult(Result.Failure<bool>(resolved.Error));
        }

        var arguments = CreateBaseArguments(resolved.Value!);
        arguments.Add("down");
        return RunMutationAsync("down", arguments, resolved.Value!.ComposeFilePath, cancellationToken);
    }

    public Task<Result<bool>> BuildAsync(
        DockerComposeRequest request,
        CancellationToken cancellationToken = default)
    {
        var resolved = ResolveRequest(request);
        if (resolved.IsFailure)
        {
            return Task.FromResult(Result.Failure<bool>(resolved.Error));
        }

        var arguments = CreateBaseArguments(resolved.Value!);
        arguments.Add("build");
        return RunMutationAsync("build", arguments, resolved.Value!.ComposeFilePath, cancellationToken);
    }

    public async Task<Result<string>> GetLogsAsync(
        DockerComposeLogsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Tail < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.Tail, "Tail must be positive.");
        }

        var resolved = ResolveRequest(
            new DockerComposeRequest(request.ComposeFilePath, request.ProjectName));
        if (resolved.IsFailure)
        {
            return Result.Failure<string>(resolved.Error);
        }

        var arguments = CreateBaseArguments(resolved.Value!);
        arguments.Add("logs");
        arguments.Add("--no-color");
        arguments.Add("--tail");
        arguments.Add(request.Tail.ToString(CultureInfo.InvariantCulture));
        var execution = await RunAsync(
            arguments,
            Path.GetDirectoryName(resolved.Value!.ComposeFilePath)!,
            cancellationToken).ConfigureAwait(false);
        if (execution.IsFailure)
        {
            return Result.Failure<string>(execution.Error);
        }

        var processResult = execution.Value!;
        return processResult.Succeeded
            ? Result.Success(processResult.StandardOutput)
            : Result.Failure<string>(
                DockerComposeErrors.CommandFailed("logs", processResult.ExitCode, NormalizeDetails(processResult)));
    }

    private async Task<Result<bool>> RunMutationAsync(
        string command,
        IReadOnlyList<string> arguments,
        string composeFilePath,
        CancellationToken cancellationToken)
    {
        var execution = await RunAsync(
            arguments,
            Path.GetDirectoryName(composeFilePath)!,
            cancellationToken).ConfigureAwait(false);
        if (execution.IsFailure)
        {
            return Result.Failure<bool>(execution.Error);
        }

        var processResult = execution.Value!;
        return processResult.Succeeded
            ? Result.Success(true)
            : Result.Failure<bool>(
                DockerComposeErrors.CommandFailed(command, processResult.ExitCode, NormalizeDetails(processResult)));
    }

    private async Task<Result<ProcessResult>> RunAsync(
        IReadOnlyList<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var execution = await _processRunner
            .RunAsync(new ProcessRequest("docker", arguments, workingDirectory), cancellationToken)
            .ConfigureAwait(false);
        return execution.IsSuccess
            ? execution
            : Result.Failure<ProcessResult>(DockerComposeErrors.ExecutionUnavailable(execution.Error.Message));
    }

    private static Result<DockerComposeRequest> ResolveRequest(DockerComposeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ComposeFilePath);
        var fullPath = Path.GetFullPath(request.ComposeFilePath);
        return File.Exists(fullPath)
            ? Result.Success(new DockerComposeRequest(fullPath, request.ProjectName, request.Detached))
            : Result.Failure<DockerComposeRequest>(DockerComposeErrors.ComposeFileUnavailable(fullPath));
    }

    private static List<string> CreateBaseArguments(DockerComposeRequest request)
    {
        var arguments = new List<string> { "compose", "--file", request.ComposeFilePath };
        if (!string.IsNullOrWhiteSpace(request.ProjectName))
        {
            arguments.Add("--project-name");
            arguments.Add(request.ProjectName.Trim());
        }

        return arguments;
    }

    private static string NormalizeDetails(ProcessResult result) =>
        !string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardError.Trim()
            : result.StandardOutput.Trim();
}
