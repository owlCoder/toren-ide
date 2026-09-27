using System.ComponentModel;
using System.Diagnostics;
using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;

namespace Toren.Platform.Execution.Adapters;

public sealed class SystemProcessRunner : IProcessRunner
{
    private const string ProcessStartErrorCode = "process.start.failed";

    public async Task<Result<ProcessResult>> RunAsync(
        ProcessRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FileName);

        var startInfo = CreateStartInfo(request);
        using var process = new Process { StartInfo = startInfo };

        try
        {
            if (!process.Start())
            {
                return Result.Failure<ProcessResult>(
                    OperationError.Create(ProcessStartErrorCode, $"Failed to start process '{request.FileName}'."));
            }
        }
        catch (Win32Exception exception)
        {
            return Result.Failure<ProcessResult>(
                OperationError.Create(
                    ProcessStartErrorCode,
                    $"Unable to start process '{request.FileName}': {exception.Message}"));
        }

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
