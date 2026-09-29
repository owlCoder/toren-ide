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
                Is.EqualTo(new[] { "pause", "next", "stepIn", "stepOut" }));
            Assert.That(
                client.Arguments.TakeLast(4),
                Has.All.Contains("\"threadId\":11"));
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

    private sealed class RecordingClient : IDebugAdapterClient
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
            return Task.FromResult(Result.Success(new DapProtocolMessage(
                Commands.Count + 100,
                DapProtocolMessageKind.Response,
                command,
                EventName: null,
                RequestSequence: Commands.Count,
                Success: true,
                Payload: $$"""
                    {"seq":{{Commands.Count + 100}},"type":"response","request_seq":{{Commands.Count}},"success":true,"command":"{{command}}"}
                    """)));
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
