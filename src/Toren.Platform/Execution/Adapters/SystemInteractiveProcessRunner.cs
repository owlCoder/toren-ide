using System.ComponentModel;
using System.Diagnostics;
using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;

namespace Toren.Platform.Execution.Adapters;

public sealed class SystemInteractiveProcessRunner : IInteractiveProcessRunner
{
    private const string ProcessStartErrorCode = "process.interactive.start.failed";

    public Task<Result<IInteractiveProcessSession>> StartAsync(
        ProcessRequest request,
        Action<ProcessOutputLine> onOutput,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FileName);
        ArgumentNullException.ThrowIfNull(onOutput);
        cancellationToken.ThrowIfCancellationRequested();

        var process = new Process { StartInfo = CreateStartInfo(request) };
        try
        {
            if (!process.Start())
            {
                process.Dispose();
                return Task.FromResult(Result.Failure<IInteractiveProcessSession>(
                    OperationError.Create(
                        ProcessStartErrorCode,
                        $"Failed to start interactive process '{request.FileName}'.")));
            }

            IInteractiveProcessSession session = new InteractiveProcessSession(process, onOutput);
            return Task.FromResult(Result.Success(session));
        }
        catch (Win32Exception exception)
        {
            process.Dispose();
            return Task.FromResult(Result.Failure<IInteractiveProcessSession>(
                OperationError.Create(
                    ProcessStartErrorCode,
                    $"Unable to start interactive process '{request.FileName}': {exception.Message}")));
        }
    }

    private static ProcessStartInfo CreateStartInfo(ProcessRequest request)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = request.FileName,
            WorkingDirectory = request.WorkingDirectory ?? string.Empty,
            UseShellExecute = false,
            RedirectStandardInput = true,
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

    private sealed class InteractiveProcessSession : IInteractiveProcessSession
    {
        private const string ProcessWriteErrorCode = "process.interactive.write.failed";
        private const string ProcessWaitErrorCode = "process.interactive.wait.failed";
        private const string ProcessTerminateErrorCode = "process.interactive.terminate.failed";
        private readonly Process _process;
        private readonly List<string> _standardOutput = [];
        private readonly List<string> _standardError = [];
        private readonly object _outputLock = new();
        private readonly Task _standardOutputReader;
        private readonly Task _standardErrorReader;
        private int _disposed;

        public InteractiveProcessSession(Process process, Action<ProcessOutputLine> onOutput)
        {
            _process = process;
            _standardOutputReader = ReadLinesAsync(
                process.StandardOutput,
                ProcessOutputChannel.StandardOutput,
                _standardOutput,
                onOutput);
            _standardErrorReader = ReadLinesAsync(
                process.StandardError,
                ProcessOutputChannel.StandardError,
                _standardError,
                onOutput);
            Completion = ObserveCompletionAsync();
        }

        public int ProcessId => _process.Id;

        public bool HasExited
        {
            get
            {
                try
                {
                    return _process.HasExited;
                }
                catch (InvalidOperationException)
                {
                    return true;
                }
            }
        }

        public Task<Result<ProcessResult>> Completion { get; }

        public async Task<Result<bool>> WriteLineAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(text);
            cancellationToken.ThrowIfCancellationRequested();
            if (HasExited)
            {
                return Result.Failure<bool>(OperationError.Create(
                    ProcessWriteErrorCode,
                    "The interactive process has already exited."));
            }

            try
            {
                await _process.StandardInput.WriteLineAsync(text.AsMemory(), cancellationToken)
                    .ConfigureAwait(false);
                await _process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
                return Result.Success(true);
            }
            catch (IOException exception)
            {
                return Result.Failure<bool>(OperationError.Create(
                    ProcessWriteErrorCode,
                    $"Unable to write to the interactive process: {exception.Message}"));
            }
            catch (ObjectDisposedException exception)
            {
                return Result.Failure<bool>(OperationError.Create(
                    ProcessWriteErrorCode,
                    $"Unable to write to the interactive process: {exception.Message}"));
            }
        }

        public async Task<Result<bool>> TerminateAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (!HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
                // The process exited between the state check and Kill.
            }
            catch (Win32Exception exception)
            {
                return Result.Failure<bool>(OperationError.Create(
                    ProcessTerminateErrorCode,
                    $"Unable to terminate the interactive process: {exception.Message}"));
            }

            await Completion.WaitAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success(true);
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            try
            {
                await TerminateAsync().ConfigureAwait(false);
            }
            finally
            {
                _process.Dispose();
            }
        }

        private async Task<Result<ProcessResult>> ObserveCompletionAsync()
        {
            try
            {
                await _process.WaitForExitAsync().ConfigureAwait(false);
                await Task.WhenAll(_standardOutputReader, _standardErrorReader).ConfigureAwait(false);
                string standardOutput;
                string standardError;
                lock (_outputLock)
                {
                    standardOutput = string.Join(Environment.NewLine, _standardOutput);
                    standardError = string.Join(Environment.NewLine, _standardError);
                }

                return Result.Success(new ProcessResult(
                    _process.ExitCode,
                    standardOutput,
                    standardError));
            }
            catch (InvalidOperationException exception)
            {
                return Result.Failure<ProcessResult>(OperationError.Create(
                    ProcessWaitErrorCode,
                    $"Unable to observe the interactive process: {exception.Message}"));
            }
        }

        private async Task ReadLinesAsync(
            StreamReader reader,
            ProcessOutputChannel channel,
            List<string> destination,
            Action<ProcessOutputLine> onOutput)
        {
            try
            {
                while (await reader.ReadLineAsync(CancellationToken.None).ConfigureAwait(false) is { } line)
                {
                    lock (_outputLock)
                    {
                        destination.Add(line);
                    }

                    onOutput(new ProcessOutputLine(channel, line));
                }
            }
            catch (IOException)
            {
                // Terminating the process can close redirected pipes while readers are completing.
            }
            catch (ObjectDisposedException)
            {
                // Disposing the process can close redirected streams while readers are completing.
            }
        }
    }
}
