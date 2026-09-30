using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.DotNet.UserSecrets.Contracts;
using Toren.DotNet.UserSecrets.Errors;
using Toren.DotNet.UserSecrets.Models;

namespace Toren.DotNet.UserSecrets.Services;

public sealed class DotNetUserSecretsService(IProcessRunner processRunner) : IDotNetUserSecretsService
{
    private readonly IProcessRunner _processRunner = processRunner
        ?? throw new ArgumentNullException(nameof(processRunner));

    public Task<Result<bool>> InitializeAsync(
        string projectPath,
        CancellationToken cancellationToken = default) =>
        RunMutationAsync(projectPath, "init", ["user-secrets", "init"], cancellationToken);

    public async Task<Result<IReadOnlyList<UserSecretEntry>>> ListAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        var project = ResolveProject(projectPath);
        if (project.IsFailure)
        {
            return Result.Failure<IReadOnlyList<UserSecretEntry>>(project.Error);
        }

        var fullProjectPath = project.Value!;
        var execution = await RunAsync(
            ["user-secrets", "list", "--project", fullProjectPath],
            Path.GetDirectoryName(fullProjectPath)!,
            cancellationToken).ConfigureAwait(false);
        if (execution.IsFailure)
        {
            return Result.Failure<IReadOnlyList<UserSecretEntry>>(execution.Error);
        }

        var processResult = execution.Value!;
        if (!processResult.Succeeded)
        {
            return Result.Failure<IReadOnlyList<UserSecretEntry>>(
                DotNetUserSecretsErrors.CommandFailed("list", processResult.ExitCode, processResult.StandardError));
        }

        return Result.Success<IReadOnlyList<UserSecretEntry>>(ParseList(processResult.StandardOutput));
    }

    public Task<Result<bool>> SetAsync(
        string projectPath,
        string key,
        string value,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        return RunMutationAsync(
            projectPath,
            "set",
            ["user-secrets", "set", key, value],
            cancellationToken);
    }

    public Task<Result<bool>> RemoveAsync(
        string projectPath,
        string key,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return RunMutationAsync(
            projectPath,
            "remove",
            ["user-secrets", "remove", key],
            cancellationToken);
    }

    public Task<Result<bool>> ClearAsync(
        string projectPath,
        CancellationToken cancellationToken = default) =>
        RunMutationAsync(projectPath, "clear", ["user-secrets", "clear"], cancellationToken);

    private async Task<Result<bool>> RunMutationAsync(
        string projectPath,
        string command,
        List<string> arguments,
        CancellationToken cancellationToken)
    {
        var project = ResolveProject(projectPath);
        if (project.IsFailure)
        {
            return Result.Failure<bool>(project.Error);
        }

        var fullProjectPath = project.Value!;
        arguments.Add("--project");
        arguments.Add(fullProjectPath);
        var execution = await RunAsync(arguments, Path.GetDirectoryName(fullProjectPath)!, cancellationToken)
            .ConfigureAwait(false);
        if (execution.IsFailure)
        {
            return Result.Failure<bool>(execution.Error);
        }

        var processResult = execution.Value!;
        return processResult.Succeeded
            ? Result.Success(true)
            : Result.Failure<bool>(
                DotNetUserSecretsErrors.CommandFailed(command, processResult.ExitCode, processResult.StandardError));
    }

    private async Task<Result<ProcessResult>> RunAsync(
        List<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var execution = await _processRunner
            .RunAsync(new ProcessRequest("dotnet", arguments, workingDirectory), cancellationToken)
            .ConfigureAwait(false);
        return execution.IsSuccess
            ? execution
            : Result.Failure<ProcessResult>(
                DotNetUserSecretsErrors.ExecutionUnavailable(execution.Error.Message));
    }

    private static Result<string> ResolveProject(string projectPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        var fullProjectPath = Path.GetFullPath(projectPath);
        return File.Exists(fullProjectPath)
            ? Result.Success(fullProjectPath)
            : Result.Failure<string>(DotNetUserSecretsErrors.ProjectUnavailable(fullProjectPath));
    }

    private static IReadOnlyList<UserSecretEntry> ParseList(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return [];
        }

        var entries = new List<UserSecretEntry>();
        foreach (var rawLine in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            var separator = line.IndexOf(" = ", StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            if (key.Length == 0)
            {
                continue;
            }

            entries.Add(new UserSecretEntry(key, line[(separator + 3)..]));
        }

        return entries;
    }
}
