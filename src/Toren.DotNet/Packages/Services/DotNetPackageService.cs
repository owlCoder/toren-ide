using System.Text.Json;
using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.DotNet.Packages.Contracts;
using Toren.DotNet.Packages.Errors;
using Toren.DotNet.Packages.Models;
using Toren.DotNet.Packages.Parsers;

namespace Toren.DotNet.Packages.Services;

public sealed class DotNetPackageService(IProcessRunner processRunner) : IDotNetPackageService
{
    private readonly IProcessRunner _processRunner = processRunner
        ?? throw new ArgumentNullException(nameof(processRunner));

    public async Task<Result<IReadOnlyList<NuGetPackageSearchResult>>> SearchAsync(
        string workingDirectory,
        string query,
        IReadOnlyList<string>? sources = null,
        int skip = 0,
        int take = 20,
        bool includePrerelease = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        ArgumentOutOfRangeException.ThrowIfLessThan(take, 1);
        if (!Directory.Exists(workingDirectory))
        {
            return Result.Failure<IReadOnlyList<NuGetPackageSearchResult>>(
                DotNetPackageErrors.WorkingDirectoryUnavailable(workingDirectory));
        }

        var arguments = new List<string>
        {
            "package",
            "search",
            query,
            "--format",
            "json",
            "--skip",
            skip.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--take",
            take.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        if (includePrerelease)
        {
            arguments.Add("--prerelease");
        }

        if (sources is not null)
        {
            foreach (var source in sources.Where(static source => !string.IsNullOrWhiteSpace(source)))
            {
                arguments.Add("--source");
                arguments.Add(source);
            }
        }

        var execution = await RunAsync(arguments, Path.GetFullPath(workingDirectory), cancellationToken)
            .ConfigureAwait(false);
        if (execution.IsFailure)
        {
            return Result.Failure<IReadOnlyList<NuGetPackageSearchResult>>(execution.Error);
        }

        var processResult = execution.Value!;
        if (!processResult.Succeeded)
        {
            return Result.Failure<IReadOnlyList<NuGetPackageSearchResult>>(
                DotNetPackageErrors.CommandFailed("search", processResult.ExitCode, processResult.StandardError));
        }

        try
        {
            return Result.Success(DotNetPackageJsonParser.ParseSearch(processResult.StandardOutput));
        }
        catch (JsonException exception)
        {
            return Result.Failure<IReadOnlyList<NuGetPackageSearchResult>>(
                DotNetPackageErrors.InvalidOutput("search", exception.Message));
        }
    }

    public async Task<Result<IReadOnlyList<NuGetInstalledPackage>>> GetInstalledAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        var project = ResolveProject(projectPath);
        if (project.IsFailure)
        {
            return Result.Failure<IReadOnlyList<NuGetInstalledPackage>>(project.Error);
        }

        var fullProjectPath = project.Value!;
        var arguments = new List<string>
        {
            "package",
            "list",
            "--project",
            fullProjectPath,
            "--format",
            "json",
            "--output-version",
            "1",
        };
        var execution = await RunAsync(arguments, Path.GetDirectoryName(fullProjectPath)!, cancellationToken)
            .ConfigureAwait(false);
        if (execution.IsFailure)
        {
            return Result.Failure<IReadOnlyList<NuGetInstalledPackage>>(execution.Error);
        }

        var processResult = execution.Value!;
        if (!processResult.Succeeded)
        {
            return Result.Failure<IReadOnlyList<NuGetInstalledPackage>>(
                DotNetPackageErrors.CommandFailed("list", processResult.ExitCode, processResult.StandardError));
        }

        try
        {
            return Result.Success(DotNetPackageJsonParser.ParseInstalled(processResult.StandardOutput));
        }
        catch (JsonException exception)
        {
            return Result.Failure<IReadOnlyList<NuGetInstalledPackage>>(
                DotNetPackageErrors.InvalidOutput("list", exception.Message));
        }
    }

    public Task<Result<bool>> AddAsync(
        string projectPath,
        string packageId,
        string? version = null,
        string? source = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        var arguments = new List<string> { "package", "add", packageId };
        if (!string.IsNullOrWhiteSpace(version))
        {
            arguments.Add("--version");
            arguments.Add(version.Trim());
        }

        if (!string.IsNullOrWhiteSpace(source))
        {
            arguments.Add("--source");
            arguments.Add(source.Trim());
        }

        return RunProjectMutationAsync(projectPath, "add", arguments, cancellationToken);
    }

    public Task<Result<bool>> UpdateAsync(
        string projectPath,
        string packageId,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        var package = string.IsNullOrWhiteSpace(version)
            ? packageId
            : $"{packageId}@{version.Trim()}";
        return RunProjectMutationAsync(
            projectPath,
            "update",
            ["package", "update", package],
            cancellationToken);
    }

    public Task<Result<bool>> RemoveAsync(
        string projectPath,
        string packageId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        return RunProjectMutationAsync(
            projectPath,
            "remove",
            ["package", "remove", packageId],
            cancellationToken);
    }

    private async Task<Result<bool>> RunProjectMutationAsync(
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
                DotNetPackageErrors.CommandFailed(command, processResult.ExitCode, processResult.StandardError));
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
            : Result.Failure<ProcessResult>(DotNetPackageErrors.ExecutionUnavailable(execution.Error.Message));
    }

    private static Result<string> ResolveProject(string projectPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        var fullProjectPath = Path.GetFullPath(projectPath);
        return File.Exists(fullProjectPath)
            ? Result.Success(fullProjectPath)
            : Result.Failure<string>(DotNetPackageErrors.ProjectUnavailable(fullProjectPath));
    }
}
