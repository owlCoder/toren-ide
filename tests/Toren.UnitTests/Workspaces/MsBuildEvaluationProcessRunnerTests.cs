using System.Collections.Concurrent;
using NUnit.Framework;
using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.UnitTests.TestDoubles;
using Toren.Workspaces.Adapters;

namespace Toren.UnitTests.Workspaces;

[TestFixture]
public sealed class MsBuildEvaluationProcessRunnerTests
{
    [Test]
    public async Task ConcurrentConsumersShareTheBudgetWhileForegroundCommandsRemainAvailable()
    {
        var inner = new GatedRunner(2);
        var runner = new MsBuildEvaluationProcessRunner(inner, maxConcurrency: 2);
        var request = new ProcessRequest("dotnet", ["msbuild", "Large.csproj", "-getItem:Compile"],
            "/work", new Dictionary<string, string?> { ["QA_MODE"] = "true" });
        var first = runner.RunAsync(request);
        var second = runner.RunAsync(request);
        await inner.BatchStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var third = runner.RunAsync(request);
        Assert.That(inner.Evaluations, Has.Count.EqualTo(2));
        Assert.That(third.IsCompleted, Is.False);

        var foreground = ProcessRequest.Create("dotnet", "build", "Large.sln");
        var build = await runner.RunAsync(foreground).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(build.IsSuccess, Is.True);
        Assert.That(inner.ForegroundRequest, Is.SameAs(foreground));

        inner.Release.SetResult();
        await Task.WhenAll(first, second, third).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(inner.MaximumActive, Is.EqualTo(2));
        Assert.That(inner.Active, Is.Zero);
        foreach (var evaluation in inner.Evaluations)
        {
            Assert.That(evaluation.Arguments, Does.Contain("-maxcpucount:1"));
            Assert.That(evaluation.Arguments, Does.Contain("-nodeReuse:false"));
            Assert.That(evaluation.WorkingDirectory, Is.EqualTo(request.WorkingDirectory));
            Assert.That(evaluation.EnvironmentVariables, Is.SameAs(request.EnvironmentVariables));
        }
    }

    [Test]
    public async Task CancellingWaitingAndRunningEvaluationsReturnsTheBudget()
    {
        var inner = new GatedRunner(1);
        var runner = new MsBuildEvaluationProcessRunner(inner, maxConcurrency: 1);
        var request = ProcessRequest.Create("dotnet", "msbuild", "Large.csproj", "-getProperty:TargetFramework");
        using var runningCancellation = new CancellationTokenSource();
        using var waitingCancellation = new CancellationTokenSource();
        var running = runner.RunAsync(request, runningCancellation.Token);
        await inner.BatchStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var waiting = runner.RunAsync(request, waitingCancellation.Token);
        waitingCancellation.Cancel();
        await Assert.CatchAsync<OperationCanceledException>(async () => await waiting);
        runningCancellation.Cancel();
        await Assert.CatchAsync<OperationCanceledException>(async () => await running);
        Assert.That(inner.Evaluations, Has.Count.EqualTo(1));
        Assert.That(inner.Active, Is.Zero);

        inner.Release.SetResult();
        var next = await runner.RunAsync(request).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(next.IsSuccess, Is.True);
        Assert.That(inner.Evaluations, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task FailedEvaluationReturnsItsSlot()
    {
        var runner = new MsBuildEvaluationProcessRunner(new FakeProcessRunner("", exitCode: 1), maxConcurrency: 1);
        var request = ProcessRequest.Create("dotnet", "msbuild", "Broken.csproj", "-getItem:Compile");
        await runner.RunAsync(request);
        var next = await runner.RunAsync(request).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(next.Value!.Succeeded, Is.False);
    }

    private sealed class GatedRunner(int batchSize) : IProcessRunner
    {
        private int _active;
        private int _maximum;
        public ConcurrentQueue<ProcessRequest> Evaluations { get; } = new();
        public ProcessRequest? ForegroundRequest { get; private set; }
        public TaskCompletionSource BatchStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Active => Volatile.Read(ref _active);
        public int MaximumActive => Volatile.Read(ref _maximum);

        public async Task<Result<ProcessResult>> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default)
        {
            if (request.Arguments[0] != "msbuild")
            {
                ForegroundRequest = request;
                return Result.Success(new ProcessResult(0, "", ""));
            }

            Evaluations.Enqueue(request);
            var active = Interlocked.Increment(ref _active);
            int previous;
            do { previous = _maximum; }
            while (active > previous && Interlocked.CompareExchange(ref _maximum, active, previous) != previous);
            if (Evaluations.Count == batchSize) BatchStarted.TrySetResult();
            try
            {
                await Release.Task.WaitAsync(cancellationToken);
                return Result.Success(new ProcessResult(0, "", ""));
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }
    }
}
