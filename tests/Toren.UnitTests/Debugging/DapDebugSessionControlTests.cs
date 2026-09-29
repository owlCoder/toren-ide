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
public sealed class DapDebugSessionControlTests
{
    private static readonly string[] ExpectedThreadControlCommands =
        ["pause", "next", "stepIn", "stepOut"];
    private static readonly string[] ExpectedRunControlCommands =
        ["gotoTargets", "goto", "restart", "disconnect"];

    [Test]
    public async Task ThreadControlsMapToDapCommands()
    {
        var client = new RecordingClient();
        var service = new DapDebugSessionService(
            new SuccessfulLocator(),
            new TransportFactory(),
            new ClientFactory(client));
        var attached = await service.AttachAsync(42);
        Assert.That(attached.IsSuccess, Is.True);
        await using var session = attached.Value!;

        var paused = await session.PauseAsync(11);
        var steppedOver = await session.StepOverAsync(11);
        var steppedInto = await session.StepIntoAsync(11);
        var steppedOut = await session.StepOutAsync(11);

        Assert.Multiple(() =>
        {
            Assert.That(paused.IsSuccess, Is.True);
            Assert.That(steppedOver.IsSuccess, Is.True);
            Assert.That(steppedInto.IsSuccess, Is.True);
            Assert.That(steppedOut.IsSuccess, Is.True);
            Assert.That(
                client.Commands.TakeLast(4),
                Is.EqualTo(ExpectedThreadControlCommands));
            Assert.That(
                client.Arguments.TakeLast(4),
                Has.All.Contains("\"threadId\":11"));
        });
    }

    [Test]
    public async Task RunToCursorRestartAndStopMapToDapCommands()
    {
        var client = new RecordingClient();
        var service = new DapDebugSessionService(
            new SuccessfulLocator(),
            new TransportFactory(),
            new ClientFactory(client));
        var attached = await service.AttachAsync(42);
        Assert.That(attached.IsSuccess, Is.True);
        await using var session = attached.Value!;

        var runToCursor = await session.RunToCursorAsync(
            11,
            "/workspace/Program.cs",
            21,
            5);
        var restarted = await session.RestartAsync();
        var stopped = await session.StopAsync();

        Assert.Multiple(() =>
        {
            Assert.That(runToCursor.IsSuccess, Is.True);
            Assert.That(restarted.IsSuccess, Is.True);
            Assert.That(stopped.IsSuccess, Is.True);
            Assert.That(client.Commands.TakeLast(4), Is.EqualTo(ExpectedRunControlCommands));
            Assert.That(client.Arguments[^4], Does.Contain("\"line\":21"));
            Assert.That(client.Arguments[^4], Does.Contain("\"column\":5"));
            Assert.That(client.Arguments[^3], Does.Contain("\"threadId\":11"));
            Assert.That(client.Arguments[^3], Does.Contain("\"targetId\":77"));
            Assert.That(client.Arguments[^1], Does.Contain("\"terminateDebuggee\":true"));
        });
    }

    [Test]
    public async Task RunToCursorFailsWhenAdapterReturnsNoTarget()
    {
        var client = new RecordingClient(returnGotoTarget: false);
        var service = new DapDebugSessionService(
            new SuccessfulLocator(),
            new TransportFactory(),
            new ClientFactory(client));
        var attached = await service.AttachAsync(42);
        Assert.That(attached.IsSuccess, Is.True);
        await using var session = attached.Value!;

        var result = await session.RunToCursorAsync(11, "/workspace/Program.cs", 21);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("debug.session.invalid-goto-targets"));
            Assert.That(client.Commands[^1], Is.EqualTo("gotoTargets"));
        });
    }

    private sealed class SuccessfulLocator : IDebugAdapterLocator
    {
        public Result<DebugAdapterDescriptor> Locate() =>
            Result.Success(new DebugAdapterDescriptor("netcoredbg", ["--interpreter=vscode"], "netcoredbg"));
    }

    private sealed class TransportFactory : IDebugAdapterTransportFactory
    {
        public Result<IDebugAdapterTransport> Start(
            DebugAdapterDescriptor adapter,
            string? workingDirectory = null) =>
            Result.Success<IDebugAdapterTransport>(new NullTransport());
    }

    private sealed class ClientFactory(RecordingClient client) : IDebugAdapterClientFactory
    {
        public IDebugAdapterClient Create(IDebugAdapterTransport transport) => client;
    }

    private sealed class RecordingClient(bool returnGotoTarget = true) : IDebugAdapterClient
    {
        public List<string> Commands { get; } = [];

        public List<string> Arguments { get; } = [];

        public Task<Result<DapProtocolMessage>> SendRequestAsync(
            string command,
            object? arguments = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Commands.Add(command);
            Arguments.Add(arguments is null ? string.Empty : JsonSerializer.Serialize(arguments));
            object? body = command == "gotoTargets"
                ? new
                {
                    targets = returnGotoTarget
                        ? new[] { new { id = 77, label = "Program.cs:21", line = 21, column = 5 } }
                        : [],
                }
                : null;
            var payload = JsonSerializer.Serialize(new
            {
                seq = Commands.Count + 100,
                type = "response",
                request_seq = Commands.Count,
                success = true,
                command,
                body,
            });
            return Task.FromResult(Result.Success(new DapProtocolMessage(
                Commands.Count + 100,
                DapProtocolMessageKind.Response,
                command,
                EventName: null,
                RequestSequence: Commands.Count,
                Success: true,
                Payload: payload)));
        }

        public IAsyncEnumerable<DapProtocolMessage> ReadNotificationsAsync(
            CancellationToken cancellationToken = default) =>
            EmptyNotifications(cancellationToken);

        public Task<int> WaitForExitAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(0);
        }

        public void Terminate()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static async IAsyncEnumerable<DapProtocolMessage> EmptyNotifications(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.CompletedTask;
            yield break;
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
