using System.Text.Json;
using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.DotNet.EntityFramework.Contracts;
using Toren.DotNet.EntityFramework.Errors;
using Toren.DotNet.EntityFramework.Models;

namespace Toren.DotNet.EntityFramework.Services;

public sealed class EfCoreToolService(IProcessRunner processRunner) : IEfCoreToolService
{
    private readonly IProcessRunner _processRunner = processRunner
        ?? throw new ArgumentNullException(nameof(processRunner));

    public async Task<Result<EfCoreToolStatus>> DetectAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        var project = ResolveProject(projectPath);
        if (project.IsFailure)
        {
            return Result.Failure<EfCoreToolStatus>(project.Error);
        }

        var execution = await RunAsync(
            ["ef", "--version"],
            Path.GetDirectoryName(project.Value!)!,
            cancellationToken).ConfigureAwait(false);
        if (execution.IsFailure)
        {
            return Result.Failure<EfCoreToolStatus>(execution.Error);
        }

        var processResult = execution.Value!;
        return Result.Success(
            new EfCoreToolStatus(
                processResult.Succeeded,
                processResult.Succeeded ? processResult.StandardOutput.Trim() : null,
                processResult.Succeeded ? null : NormalizeDetails(processResult)));
    }

    public async Task<Result<IReadOnlyList<EfCoreMigrationInfo>>> ListMigrationsAsync(
        EfCoreProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        var resolved = ResolveRequest(request);
        if (resolved.IsFailure)
        {
            return Result.Failure<IReadOnlyList<EfCoreMigrationInfo>>(resolved.Error);
        }

        var arguments = new List<string> { "ef", "migrations", "list", "--json" };
        AddProjectOptions(arguments, resolved.Value!);
        var execution = await RunAsync(
            arguments,
            Path.GetDirectoryName(resolved.Value!.ProjectPath)!,
            cancellationToken).ConfigureAwait(false);
        if (execution.IsFailure)
        {
            return Result.Failure<IReadOnlyList<EfCoreMigrationInfo>>(execution.Error);
        }

        var processResult = execution.Value!;
        if (!processResult.Succeeded)
        {
            return Result.Failure<IReadOnlyList<EfCoreMigrationInfo>>(
                EfCoreToolErrors.CommandFailed("migrations list", processResult.ExitCode, NormalizeDetails(processResult)));
        }

        return ParseMigrationList(processResult.StandardOutput);
    }

    public async Task<Result<bool>> AddMigrationAsync(
        EfCoreMigrationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MigrationName);
        var resolved = ResolveRequest(
            new EfCoreProjectRequest(request.ProjectPath, request.StartupProjectPath, request.Context));
        if (resolved.IsFailure)
        {
            return Result.Failure<bool>(resolved.Error);
        }

        var arguments = new List<string> { "ef", "migrations", "add", request.MigrationName.Trim() };
        AddProjectOptions(arguments, resolved.Value!);
        AddOption(arguments, "--output-dir", request.OutputDirectory);
        return await RunMutationAsync("migrations add", arguments, resolved.Value!.ProjectPath, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Result<bool>> RemoveMigrationAsync(
        EfCoreProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        var resolved = ResolveRequest(request);
        if (resolved.IsFailure)
        {
            return Result.Failure<bool>(resolved.Error);
        }

        var arguments = new List<string> { "ef", "migrations", "remove" };
        AddProjectOptions(arguments, resolved.Value!);
        return await RunMutationAsync("migrations remove", arguments, resolved.Value!.ProjectPath, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Result<bool>> UpdateDatabaseAsync(
        EfCoreDatabaseUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var resolved = ResolveRequest(
            new EfCoreProjectRequest(request.ProjectPath, request.StartupProjectPath, request.Context));
        if (resolved.IsFailure)
        {
            return Result.Failure<bool>(resolved.Error);
        }

        var arguments = new List<string> { "ef", "database", "update" };
        if (!string.IsNullOrWhiteSpace(request.Migration))
        {
            arguments.Add(request.Migration.Trim());
        }

        AddProjectOptions(arguments, resolved.Value!);
        return await RunMutationAsync("database update", arguments, resolved.Value!.ProjectPath, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<Result<bool>> RunMutationAsync(
        string command,
        IReadOnlyList<string> arguments,
        string projectPath,
        CancellationToken cancellationToken)
    {
        var execution = await RunAsync(
            arguments,
            Path.GetDirectoryName(projectPath)!,
            cancellationToken).ConfigureAwait(false);
        if (execution.IsFailure)
        {
            return Result.Failure<bool>(execution.Error);
        }

        var processResult = execution.Value!;
        return processResult.Succeeded
            ? Result.Success(true)
            : Result.Failure<bool>(
                EfCoreToolErrors.CommandFailed(command, processResult.ExitCode, NormalizeDetails(processResult)));
    }

    private async Task<Result<ProcessResult>> RunAsync(
        IReadOnlyList<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var execution = await _processRunner
            .RunAsync(new ProcessRequest("dotnet", arguments, workingDirectory), cancellationToken)
            .ConfigureAwait(false);
        return execution.IsSuccess
            ? execution
            : Result.Failure<ProcessResult>(EfCoreToolErrors.ExecutionUnavailable(execution.Error.Message));
    }

    private static Result<EfCoreProjectRequest> ResolveRequest(EfCoreProjectRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var project = ResolveProject(request.ProjectPath);
        if (project.IsFailure)
        {
            return Result.Failure<EfCoreProjectRequest>(project.Error);
        }

        string? startupProject = null;
        if (!string.IsNullOrWhiteSpace(request.StartupProjectPath))
        {
            var startup = ResolveProject(request.StartupProjectPath);
            if (startup.IsFailure)
            {
                return Result.Failure<EfCoreProjectRequest>(startup.Error);
            }

            startupProject = startup.Value!;
        }

        return Result.Success(
            new EfCoreProjectRequest(project.Value!, startupProject, request.Context));
    }

    private static Result<string> ResolveProject(string projectPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        var fullPath = Path.GetFullPath(projectPath);
        return File.Exists(fullPath)
            ? Result.Success(fullPath)
            : Result.Failure<string>(EfCoreToolErrors.ProjectUnavailable(fullPath));
    }

    private static void AddProjectOptions(List<string> arguments, EfCoreProjectRequest request)
    {
        arguments.Add("--project");
        arguments.Add(request.ProjectPath);
        AddOption(arguments, "--startup-project", request.StartupProjectPath);
        AddOption(arguments, "--context", request.Context);
    }

    private static void AddOption(List<string> arguments, string option, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        arguments.Add(option);
        arguments.Add(value.Trim());
    }

    private static Result<IReadOnlyList<EfCoreMigrationInfo>> ParseMigrationList(string output)
    {
        try
        {
            var start = output.IndexOf('[', StringComparison.Ordinal);
            var end = output.LastIndexOf(']');
            if (start < 0 || end < start)
            {
                return Result.Failure<IReadOnlyList<EfCoreMigrationInfo>>(
                    EfCoreToolErrors.InvalidMigrationOutput("The JSON array was not found."));
            }

            using var document = JsonDocument.Parse(output[start..(end + 1)]);
            var migrations = new List<EfCoreMigrationInfo>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                var id = GetString(element, "id") ?? string.Empty;
                var name = GetString(element, "name") ?? id;
                bool? applied = null;
                if (element.TryGetProperty("applied", out var appliedElement)
                    && appliedElement.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    applied = appliedElement.GetBoolean();
                }

                if (!string.IsNullOrWhiteSpace(id) || !string.IsNullOrWhiteSpace(name))
                {
                    migrations.Add(new EfCoreMigrationInfo(id, name, applied));
                }
            }

            return Result.Success<IReadOnlyList<EfCoreMigrationInfo>>(migrations);
        }
        catch (JsonException exception)
        {
            return Result.Failure<IReadOnlyList<EfCoreMigrationInfo>>(
                EfCoreToolErrors.InvalidMigrationOutput(exception.Message));
        }
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static string NormalizeDetails(ProcessResult result) =>
        !string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardError.Trim()
            : result.StandardOutput.Trim();
}
