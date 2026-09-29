using System.ComponentModel;
using System.Diagnostics;
using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;

namespace Toren.Platform.Execution.Adapters;

public sealed class SystemProcessRunner : IStreamingProcessRunner
{
    private const string ProcessStartErrorCode = "process.start.failed";

    public async Task<Result<ProcessResult>> RunAsync(
        ProcessRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FileName);

        var processStart = StartProcess(request);
        if (!processStart.IsSuccess)
        {
            return Result.Failure<ProcessResult>(processStart.Error);
        }

        using var process = processStart.Value;

        // Output readers intentionally use CancellationToken.None. Cancellation is handled by
        // WaitForExitAsync below, which terminates the full process tree before the method exits.
        var standardOutput = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var standardError = process.StandardError.ReadToEndAsync(CancellationToken.None);

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        var processResult = new ProcessResult(
            process.ExitCode,
            await standardOutput.ConfigureAwait(false),
            await standardError.ConfigureAwait(false));

        return Result.Success(processResult);
    }

    public async Task<Result<ProcessResult>> RunStreamingAsync(
        ProcessRequest request,
        Action<ProcessOutputLine> onOutput,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FileName);
        ArgumentNullException.ThrowIfNull(onOutput);

        var processStart = StartProcess(request);
        if (!processStart.IsSuccess)
        {
            return Result.Failure<ProcessResult>(processStart.Error);
        }

        using var process = processStart.Value;
        var standardOutput = ReadLinesAsync(
            process.StandardOutput,
            ProcessOutputStream.StandardOutput,
            onOutput);
        var standardError = ReadLinesAsync(
            process.StandardError,
            ProcessOutputStream.StandardError,
            onOutput);

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            await ObserveReadersAfterCancellationAsync(standardOutput, standardError).ConfigureAwait(false);
            throw;
        }

        var processResult = new ProcessResult(
            process.ExitCode,
            await standardOutput.ConfigureAwait(false),
            await standardError.ConfigureAwait(false));
        return Result.Success(processResult);
    }

    private static Result<Process> StartProcess(ProcessRequest request)
    {
        var process = new Process { StartInfo = CreateStartInfo(request) };
        try
        {
            if (process.Start())
            {
                return Result.Success(process);
            }

            process.Dispose();
            return Result.Failure<Process>(
                OperationError.Create(ProcessStartErrorCode, $"Failed to start process '{request.FileName}'."));
        }
        catch (Win32Exception exception)
        {
            process.Dispose();
            return Result.Failure<Process>(
                OperationError.Create(
                    ProcessStartErrorCode,
                    $"Unable to start process '{request.FileName}': {exception.Message}"));
        }
    }

    private static async Task<string> ReadLinesAsync(
        StreamReader reader,
        ProcessOutputStream stream,
        Action<ProcessOutputLine> onOutput)
    {
        var lines = new List<string>();
        while (await reader.ReadLineAsync(CancellationToken.None).ConfigureAwait(false) is { } line)
        {
            lines.Add(line);
            onOutput(new ProcessOutputLine(stream, line));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static async Task ObserveReadersAfterCancellationAsync(
        Task<string> standardOutput,
        Task<string> standardError)
    {
        try
        {
            await Task.WhenAll(standardOutput, standardError).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // The killed process can close redirected pipes while a reader is completing.
        }
        catch (ObjectDisposedException)
        {
            // The killed process can dispose redirected streams while a reader is completing.
        }
    }

    private static ProcessStartInfo CreateStartInfo(ProcessRequest request)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = request.FileName,
            WorkingDirectory = request.WorkingDirectory ?? string.Empty,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (request.EnvironmentVariables is null)
        {
            return startInfo;
        }

        foreach (var (key, value) in request.EnvironmentVariables)
        {
            if (value is null)
            {
                startInfo.Environment.Remove(key);
            }
            else
            {
                startInfo.Environment[key] = value;
            }
        }

        return startInfo;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process exited between the HasExited check and Kill.
        }
    }
}
