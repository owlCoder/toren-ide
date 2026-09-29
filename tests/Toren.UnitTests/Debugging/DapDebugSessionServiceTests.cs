using System.Runtime.CompilerServices;
using System.Text.Json;
using NUnit.Framework;
using Toren.Core.Results;
using Toren.Debugging.Contracts;
using Toren.Debugging.Models;
using Toren.Debugging.Protocol;
using Toren.Debugging.Services;

namespace Toren.UnitTests.Debugging;

[TestFixture]
public sealed class DapDebugSessionServiceTests
{
    private static readonly string[] ExpectedAttachCommands =
        ["initialize", "attach", "configurationDone"];
    private static readonly string[] ExpectedAdapterArguments =
        ["--interpreter=vscode"];

    [Test]
    public async Task AttachInitializesAdapterAndCreatesProcessSession()
    {
        var client = new RecordingDebugAdapterClient();
        var transportFactory = new RecordingTransportFactory();
        var service = new DapDebugSessionService(
            new SuccessfulLocator(),
            transportFactory,
            new RecordingClientFactory(client));

        var result = await service.AttachAsync(42, "/workspace");

        Assert.That(result.IsSuccess, Is.True);
        await using var session = result.Value!;
        Assert.Multiple(() =>
        {
            Assert.That(session.ProcessId, Is.EqualTo(42));
            Assert.That(client.Commands, Is.EqualTo(ExpectedAttachCommands));
            Assert.That(client.Arguments[1], Does.Contain("\"processId\":42"));
            Assert.That(transportFactory.WorkingDirectory, Is.EqualTo("/workspace"));
        });
    }

    [Test]
    public async Task SessionParsesStoppedEventAndContinuesThread()
    {
        var stopped = new DapProtocolMessage(
            7,
            DapProtocolMessageKind.Event,
            Command: null,
            EventName: "stopped",
            RequestSequence: null,
            Success: null,
            Payload: """
                {"seq":7,"type":"event","event":"stopped","body":{"reason":"breakpoint","threadId":11}}
                """);
        var client = new RecordingDebugAdapterClient(stopped);
        var service = new DapDebugSessionService(
            new SuccessfulLocator(),
            new RecordingTransportFactory(),
            new RecordingClientFactory(client));
        var attached = await service.AttachAsync(42);
        Assert.That(attached.IsSuccess, Is.True);
        await using var session = attached.Value!;

        var stop = await session.WaitForStopAsync();
        Assert.That(stop.IsSuccess, Is.True);
        var continued = await session.ContinueAsync(stop.Value!.ThreadId);

        Assert.Multiple(() =>
        {
            Assert.That(stop.Value!.ThreadId, Is.EqualTo(11));
            Assert.That(stop.Value.Reason, Is.EqualTo("breakpoint"));
            Assert.That(continued.IsSuccess, Is.True);
            Assert.That(client.Commands[^1], Is.EqualTo("continue"));
            Assert.That(client.Arguments[^1], Does.Contain("\"threadId\":11"));
        });
    }

    [Test]
    public async Task AttachPropagatesLocatorFailureWithoutStartingTransport()
    {
        var transportFactory = new RecordingTransportFactory();
        var service = new DapDebugSessionService(
            new FailingLocator(),
            transportFactory,
            new RecordingClientFactory(new RecordingDebugAdapterClient()));

        var result = await service.AttachAsync(42);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("debug.adapter.not-found"));
            Assert.That(transportFactory.StartCount, Is.Zero);
        });
    }

    [Test]
    public async Task DisconnectPreventsForcedAdapterTerminationOnDispose()
    {
        var client = new RecordingDebugAdapterClient();
        var service = new DapDebugSessionService(
            new SuccessfulLocator(),
            new RecordingTransportFactory(),
            new RecordingClientFactory(client));
        var attached = await service.AttachAsync(42);
        Assert.That(attached.IsSuccess, Is.True);
        var session = attached.Value!;

        var disconnected = await session.DisconnectAsync();
        await session.DisposeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(disconnected.IsSuccess, Is.True);
            Assert.That(client.Commands[^1], Is.EqualTo("disconnect"));
            Assert.That(client.IsTerminated, Is.False);
            Assert.That(client.IsDisposed, Is.True);
        });
    }

    private sealed class SuccessfulLocator : IDebugAdapterLocator
    {
        public Result<DebugAdapterDescriptor> Locate() =>
            Result.Success(new DebugAdapterDescriptor("netcoredbg", ExpectedAdapterArguments, "netcoredbg"));
    }

    private sealed class FailingLocator : IDebugAdapterLocator
    {
        public Result<DebugAdapterDescriptor> Locate() =>
            Result.Failure<DebugAdapterDescriptor>(
                OperationError.Create("debug.adapter.not-found", "missing"));
    }

    private sealed class RecordingTransportFactory : IDebugAdapterTransportFactory
    {
        public int StartCount { get; private set; }

        public string? WorkingDirectory { get; private set; }

        public Result<IDebugAdapterTransport> Start(
            DebugAdapterDescriptor adapter,
            string? workingDirectory = null)
        {
            StartCount++;
            WorkingDirectory = workingDirectory;
            return Result.Success<IDebugAdapterTransport>(new NullTransport());
        }
    }

    private sealed class RecordingClientFactory(RecordingDebugAdapterClient client) : IDebugAdapterClientFactory
    {
        public IDebugAdapterClient Create(IDebugAdapterTransport transport) => client;
    }

    private sealed class RecordingDebugAdapterClient(params DapProtocolMessage[] notifications) : IDebugAdapterClient
    {
        private readonly DapProtocolMessage[] _notifications = notifications;

        public List<string> Commands { get; } = [];

        public List<string> Arguments { get; } = [];

        public bool IsTerminated { get; private set; }

        public bool IsDisposed { get; private set; }

        public Task<Result<DapProtocolMessage>> SendRequestAsync(
            string command,
            object? arguments = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Commands.Add(command);
            Arguments.Add(arguments is null ? string.Empty : JsonSerializer.Serialize(arguments));
            var response = new DapProtocolMessage(
                Commands.Count + 100,
                DapProtocolMessageKind.Response,
                command,
                EventName: null,
                RequestSequence: Commands.Count,
                Success: true,
                Payload: $$"""
                    {"seq":{{Commands.Count + 100}},"type":"response","request_seq":{{Commands.Count}},"success":true,"command":"{{command}}"}
                    """);
            return Task.FromResult(Result.Success(response));
        }

        public IAsyncEnumerable<DapProtocolMessage> ReadNotificationsAsync(
            CancellationToken cancellationToken = default) =>
            EnumerateNotificationsAsync(cancellationToken);

        public Task<int> WaitForExitAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(0);
        }

        public void Terminate()
        {
            IsTerminated = true;
        }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }

        private async IAsyncEnumerable<DapProtocolMessage> EnumerateNotificationsAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var notification in _notifications)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return notification;
                await Task.Yield();
            }
        }
    }

    private sealed class NullTransport : IDebugAdapterTransport
    {
        public Stream ReadStream => Stream.Null;

        public Stream WriteStream => Stream.Null;

        public TextReader ErrorReader => TextReader.Null;

        public Task<int> WaitForExitAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(0);
        }

        public void Terminate()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
