using System.ComponentModel;
using System.Diagnostics;
using Toren.Core.Results;
using Toren.Debugging.Contracts;
using Toren.Debugging.Models;

namespace Toren.Debugging.Adapters;

public sealed class StdioDebugAdapterTransportFactory : IDebugAdapterTransportFactory
{
    private const string StartFailedErrorCode = "debug.adapter.start-failed";

    public Result<IDebugAdapterTransport> Start(
        DebugAdapterDescriptor adapter,
        string? workingDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentException.ThrowIfNullOrWhiteSpace(adapter.FileName);

        var startInfo = new ProcessStartInfo
        {
            FileName = adapter.FileName,
            WorkingDirectory = workingDirectory ?? string.Empty,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        foreach (var argument in adapter.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                process.Dispose();
                return Result.Failure<IDebugAdapterTransport>(
                    OperationError.Create(
                        StartFailedErrorCode,
                        $"Failed to start debug adapter '{adapter.DisplayName}'."));
            }

            return Result.Success<IDebugAdapterTransport>(new ProcessDebugAdapterTransport(process));
        }
        catch (Win32Exception exception)
        {
            process.Dispose();
            return Result.Failure<IDebugAdapterTransport>(
                OperationError.Create(
                    StartFailedErrorCode,
                    $"Unable to start debug adapter '{adapter.DisplayName}': {exception.Message}"));
        }
    }

    private sealed class ProcessDebugAdapterTransport(Process process) : IDebugAdapterTransport
    {
        private readonly Process _process = process;

        public Stream ReadStream => _process.StandardOutput.BaseStream;

        public Stream WriteStream => _process.StandardInput.BaseStream;

        public TextReader ErrorReader => _process.StandardError;

        public async Task<int> WaitForExitAsync(CancellationToken cancellationToken = default)
        {
            await _process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return _process.ExitCode;
        }

        public void Stop()
        {
            TryKill(_process);
        }

        public ValueTask DisposeAsync()
        {
            TryKill(_process);
            _process.Dispose();
            return ValueTask.CompletedTask;
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
                // The adapter exited between the HasExited check and Kill.
            }
        }
    }
}
