using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.DotNet.Execution.Contracts;
using Toren.DotNet.Execution.Errors;
using Toren.DotNet.Execution.Models;

namespace Toren.DotNet.Execution.Services;

public sealed class DotNetCommandService(IProcessRunner processRunner) : IDotNetCommandService
{
    private readonly IProcessRunner _processRunner = processRunner
        ?? throw new ArgumentNullException(nameof(processRunner));

    public async Task<Result<DotNetCommandResult>> ExecuteAsync(
        DotNetCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WorkingDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(request.WorkingDirectory))
        {
            return Result.Failure<DotNetCommandResult>(
                DotNetCommandErrors.WorkingDirectoryUnavailable(request.WorkingDirectory));
        }

        var processRequest = new ProcessRequest(
            "dotnet",
            CreateArguments(request),
            request.WorkingDirectory);
        var execution = await _processRunner
            .RunAsync(processRequest, cancellationToken)
            .ConfigureAwait(false);

        if (!execution.IsSuccess)
        {
            return Result.Failure<DotNetCommandResult>(
                DotNetCommandErrors.ExecutionUnavailable(request.Kind, execution.Error.Message));
        }

        return Result.Success(
            new DotNetCommandResult(
                request.Kind,
                execution.Value.ExitCode,
                execution.Value.StandardOutput,
                execution.Value.StandardError));
    }

    private static List<string> CreateArguments(DotNetCommandRequest request)
    {
        var arguments = new List<string>();
        switch (request.Kind)
        {
            case DotNetCommandKind.Restore:
                arguments.Add("restore");
                break;
            case DotNetCommandKind.Build:
                arguments.Add("build");
                break;
            case DotNetCommandKind.Rebuild:
                arguments.Add("build");
                break;
            case DotNetCommandKind.Clean:
                arguments.Add("clean");
                break;
            case DotNetCommandKind.Run:
                arguments.Add("run");
                break;
            case DotNetCommandKind.Publish:
                arguments.Add("publish");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(request), request.Kind, "Unsupported .NET command kind.");
        }

        AddTarget(arguments, request);

        if (request.Kind == DotNetCommandKind.Rebuild)
        {
            arguments.Add("--no-incremental");
        }

        if (request.Kind != DotNetCommandKind.Restore)
        {
            AddOption(arguments, "--configuration", request.Configuration);
            AddOption(arguments, "--framework", request.TargetFramework);
        }

        if (request.NoRestore && request.Kind is DotNetCommandKind.Build
            or DotNetCommandKind.Rebuild
            or DotNetCommandKind.Run
            or DotNetCommandKind.Publish)
        {
            arguments.Add("--no-restore");
        }

        return arguments;
    }

    private static void AddTarget(List<string> arguments, DotNetCommandRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TargetPath))
        {
            return;
        }

        if (request.Kind == DotNetCommandKind.Run)
        {
            arguments.Add("--project");
        }

        arguments.Add(request.TargetPath);
    }

    private static void AddOption(List<string> arguments, string option, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        arguments.Add(option);
        arguments.Add(value);
    }
}
