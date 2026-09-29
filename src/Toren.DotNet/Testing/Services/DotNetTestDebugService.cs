using System.Globalization;
using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.DotNet.Testing.Contracts;
using Toren.DotNet.Testing.Models;

namespace Toren.DotNet.Testing.Services;

public sealed class DotNetTestDebugService(IStreamingProcessRunner processRunner) : IDotNetTestDebugService
{
    private const string SelectionRequiredErrorCode = "dotnet.test-debug.selection-required";
    private const string MtpSelectionIdentityRequiredErrorCode = "dotnet.test-debug.mtp-selection-identity-required";
    private const string ProcessIdNotFoundErrorCode = "dotnet.test-debug.pid-not-found";
    private const string VstestDebugEnvironmentVariable = "VSTEST_HOST_DEBUG";
    private const string MtpDebugEnvironmentVariable = "TESTINGPLATFORM_WAIT_ATTACH_DEBUGGER";

    private readonly IStreamingProcessRunner _processRunner = processRunner
        ?? throw new ArgumentNullException(nameof(processRunner));

    public async Task<Result<IDotNetTestDebugSession>> StartAsync(
        DotNetTestRunRequest request,
        Action<ProcessOutputLine> onOutput,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ProjectPath);
        ArgumentNullException.ThrowIfNull(onOutput);

        var projectPath = Path.GetFullPath(request.ProjectPath);
        var runner = DotNetTestRunnerResolver.Resolve(projectPath);
        if (runner == DotNetTestRunner.MicrosoftTestingPlatform)
        {
            if (string.IsNullOrWhiteSpace(request.RunnerId))
            {
                return Result.Failure<IDotNetTestDebugSession>(
                    OperationError.Create(
                        MtpSelectionIdentityRequiredErrorCode,
                        "Debugging one Microsoft Testing Platform test requires its discovered MTP test identity."));
            }
        }
        else if (string.IsNullOrWhiteSpace(request.FullyQualifiedName))
        {
            return Result.Failure<IDotNetTestDebugSession>(
                OperationError.Create(
                    SelectionRequiredErrorCode,
                    "Debug Test requires a selected test."));
        }

        var processRequest = CreateProcessRequest(request, projectPath, runner);
        var processIdSource = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var runTask = _processRunner.RunStreamingAsync(
            processRequest,
            line =>
            {
                onOutput(line);
                if (TryParseProcessId(line.Text, out var processId))
                {
                    processIdSource.TrySetResult(processId);
                }
            },
            runCancellation.Token);

        try
        {
            _ = await Task.WhenAny(processIdSource.Task, runTask).ConfigureAwait(false);
            if (processIdSource.Task.IsCompletedSuccessfully)
            {
                return Result.Success<IDotNetTestDebugSession>(
                    new DotNetTestDebugSession(
                        processIdSource.Task.Result,
                        runTask,
                        runCancellation));
            }

            var runResult = await runTask.ConfigureAwait(false);
            runCancellation.Dispose();
            if (runResult.IsFailure)
            {
                return Result.Failure<IDotNetTestDebugSession>(runResult.Error);
            }

            return Result.Failure<IDotNetTestDebugSession>(
                OperationError.Create(
                    ProcessIdNotFoundErrorCode,
                    "The test runner exited before Toren received a debugger process id."));
        }
        catch (OperationCanceledException)
        {
            runCancellation.Cancel();
            runCancellation.Dispose();
            throw;
        }
    }

    private static ProcessRequest CreateProcessRequest(
        DotNetTestRunRequest request,
        string projectPath,
        DotNetTestRunner runner)
    {
        var arguments = new List<string> { "test" };
        if (runner == DotNetTestRunner.MicrosoftTestingPlatform)
        {
            arguments.Add("--project");
            arguments.Add(projectPath);
            arguments.Add("--no-ansi");
            arguments.Add("--no-progress");
        }
        else
        {
            arguments.Add(projectPath);
        }

        if (!string.IsNullOrWhiteSpace(request.Configuration))
        {
            arguments.Add("--configuration");
            arguments.Add(request.Configuration);
        }

        if (!string.IsNullOrWhiteSpace(request.TargetFramework))
        {
            arguments.Add("--framework");
            arguments.Add(request.TargetFramework);
        }

        if (runner == DotNetTestRunner.MicrosoftTestingPlatform)
        {
            arguments.Add("--");
            arguments.Add("--filter-uid");
            arguments.Add(request.RunnerId!);
        }
        else
        {
            arguments.Add("--filter");
            arguments.Add($"FullyQualifiedName={request.FullyQualifiedName}");
        }

        var environment = new Dictionary<string, string?>
        {
            [runner == DotNetTestRunner.MicrosoftTestingPlatform
                ? MtpDebugEnvironmentVariable
                : VstestDebugEnvironmentVariable] = "1",
        };

        return new ProcessRequest(
            "dotnet",
            arguments,
            Path.GetDirectoryName(projectPath),
            environment);
    }

    private static bool TryParseProcessId(string text, out int processId)
    {
        const string marker = "Process Id:";
        var markerIndex = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            processId = 0;
            return false;
        }

        var value = text.AsSpan(markerIndex + marker.Length).TrimStart();
        var commaIndex = value.IndexOf(',');
        if (commaIndex >= 0)
        {
            value = value[..commaIndex];
        }

        value = value.Trim();
        return int.TryParse(
            value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out processId)
            && processId > 0;
    }

    private sealed class DotNetTestDebugSession(
        int processId,
        Task<Result<ProcessResult>> completion,
        CancellationTokenSource cancellation) : IDotNetTestDebugSession
    {
        private readonly CancellationTokenSource _cancellation = cancellation;
        private int _disposeState;

        public int ProcessId { get; } = processId;

        public Task<Result<ProcessResult>> Completion { get; } = completion;

        public void Terminate()
        {
            if (Volatile.Read(ref _disposeState) == 0)
            {
                _cancellation.Cancel();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposeState, 1) != 0)
            {
                return;
            }

            _cancellation.Cancel();
            try
            {
                await Completion.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
            {
                // Normal termination of the paused/running test process.
            }
            finally
            {
                _cancellation.Dispose();
            }
        }
    }
}
