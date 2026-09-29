using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.DotNet.Execution.Contracts;
using Toren.DotNet.Execution.Errors;
using Toren.DotNet.Execution.Models;

namespace Toren.DotNet.Execution.Services;

public sealed class DotNetCommandService(IProcessRunner processRunner) : IStreamingDotNetCommandService
{
    private readonly IProcessRunner _processRunner = processRunner
        ?? throw new ArgumentNullException(nameof(processRunner));

    public async Task<Result<DotNetCommandResult>> ExecuteAsync(
        DotNetCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        var processRequest = CreateProcessRequest(request, cancellationToken);
        if (!processRequest.IsSuccess)
        {
            return Result.Failure<DotNetCommandResult>(processRequest.Error);
        }

        var execution = await _processRunner
            .RunAsync(processRequest.Value, cancellationToken)
            .ConfigureAwait(false);
        return MapResult(request.Kind, execution);
    }

    public async Task<Result<DotNetCommandResult>> ExecuteStreamingAsync(
        DotNetCommandRequest request,
        Action<DotNetCommandOutputLine> onOutput,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onOutput);
        var processRequest = CreateProcessRequest(request, cancellationToken);
        if (!processRequest.IsSuccess)
        {
            return Result.Failure<DotNetCommandResult>(processRequest.Error);
        }

        if (_processRunner is not IStreamingProcessRunner streamingProcessRunner)
        {
            var bufferedResult = await ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
            if (bufferedResult.IsSuccess)
            {
                ReportBufferedOutput(bufferedResult.Value, onOutput);
            }

            return bufferedResult;
        }

        var execution = await streamingProcessRunner
            .RunStreamingAsync(
                processRequest.Value,
                line => onOutput(new DotNetCommandOutputLine(
                    line.Channel == ProcessOutputChannel.StandardError
                        ? DotNetCommandOutputChannel.StandardError
                        : DotNetCommandOutputChannel.StandardOutput,
                    line.Text)),
                cancellationToken)
            .ConfigureAwait(false);
        return MapResult(request.Kind, execution);
    }

    private static Result<ProcessRequest> CreateProcessRequest(
        DotNetCommandRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WorkingDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        return !Directory.Exists(request.WorkingDirectory)
            ? Result.Failure<ProcessRequest>(
                DotNetCommandErrors.WorkingDirectoryUnavailable(request.WorkingDirectory))
            : Result.Success(
                new ProcessRequest(
                    "dotnet",
                    CreateArguments(request),
                    request.WorkingDirectory));
    }

    private static Result<DotNetCommandResult> MapResult(
        DotNetCommandKind kind,
        Result<ProcessResult> execution)
    {
        if (!execution.IsSuccess)
        {
            return Result.Failure<DotNetCommandResult>(
                DotNetCommandErrors.ExecutionUnavailable(kind, execution.Error.Message));
        }

        return Result.Success(
            new DotNetCommandResult(
                kind,
                execution.Value.ExitCode,
                execution.Value.StandardOutput,
                execution.Value.StandardError));
    }

    private static void ReportBufferedOutput(
        DotNetCommandResult result,
        Action<DotNetCommandOutputLine> onOutput)
    {
        ReportLines(result.StandardOutput, DotNetCommandOutputChannel.StandardOutput, onOutput);
        ReportLines(result.StandardError, DotNetCommandOutputChannel.StandardError, onOutput);
    }

    private static void ReportLines(
        string output,
        DotNetCommandOutputChannel channel,
        Action<DotNetCommandOutputLine> onOutput)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return;
        }

        foreach (var line in output.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                onOutput(new DotNetCommandOutputLine(channel, line));
            }
        }
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

        if (request.Kind == DotNetCommandKind.Run)
        {
            AddOption(arguments, "--launch-profile", request.LaunchProfile);
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
