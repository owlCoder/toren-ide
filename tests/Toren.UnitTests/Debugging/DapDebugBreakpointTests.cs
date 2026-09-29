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
public sealed class DapDebugBreakpointTests
{
    private static readonly DebugSourceBreakpoint[] RequestedBreakpoints =
        [new(12), new(24, "count > 3")];

    [Test]
    public async Task SetBreakpointsMapsConditionalRequestsAndVerificationResults()
    {
        var client = new BreakpointClient();
        var service = new DapDebugSessionService(
            new SuccessfulLocator(),
            new TransportFactory(),
            new ClientFactory(client));
        var attached = await service.AttachAsync(42);
        Assert.That(attached.IsSuccess, Is.True);
        await using var session = attached.Value!;

        var result = await session.SetBreakpointsAsync("/workspace/Program.cs", RequestedBreakpoints);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(client.LastArguments, Is.Not.Null);
        using var argumentsDocument = JsonDocument.Parse(client.LastArguments!);
        var root = argumentsDocument.RootElement;
        var requestBreakpoints = root.GetProperty("breakpoints");
        Assert.Multiple(() =>
        {
            Assert.That(client.LastCommand, Is.EqualTo("setBreakpoints"));
            Assert.That(root.GetProperty("source").GetProperty("path").GetString(), Is.EqualTo("/workspace/Program.cs"));
            Assert.That(requestBreakpoints.GetArrayLength(), Is.EqualTo(2));
            Assert.That(requestBreakpoints[0].GetProperty("line").GetInt32(), Is.EqualTo(12));
            Assert.That(requestBreakpoints[1].GetProperty("line").GetInt32(), Is.EqualTo(24));
            Assert.That(requestBreakpoints[1].GetProperty("condition").GetString(), Is.EqualTo("count > 3"));
            Assert.That(result.Value, Has.Count.EqualTo(2));
            Assert.That(result.Value![0], Is.EqualTo(new DebugBreakpoint(7, true, 12)));
            Assert.That(
                result.Value[1],
                Is.EqualTo(new DebugBreakpoint(8, false, 25, "Breakpoint moved")));
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

    private sealed class ClientFactory(BreakpointClient client) : IDebugAdapterClientFactory
    {
        public IDebugAdapterClient Create(IDebugAdapterTransport transport) => client;
    }

    private sealed class BreakpointClient : IDebugAdapterClient
    {
        private int _sequence;

        public string? LastCommand { get; private set; }

        public string? LastArguments { get; private set; }

        public Task<Result<DapProtocolMessage>> SendRequestAsync(
            string command,
            object? arguments = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _sequence++;
            LastCommand = command;
            LastArguments = arguments is null ? string.Empty : JsonSerializer.Serialize(arguments);
            var payload = string.Equals(command, "setBreakpoints", StringComparison.Ordinal)
                ? CreateBreakpointResponsePayload()
                : JsonSerializer.Serialize(new
                {
                    seq = _sequence + 100,
                    type = "response",
                    request_seq = _sequence,
                    success = true,
                    command,
                });
            return Task.FromResult(Result.Success(new DapProtocolMessage(
                _sequence + 100,
                DapProtocolMessageKind.Response,
                command,
                EventName: null,
                RequestSequence: _sequence,
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

        private string CreateBreakpointResponsePayload()
        {
            var breakpoints = new List<object>
            {
                new { id = 7, verified = true, line = 12, message = (string?)null },
                new { id = 8, verified = false, line = 25, message = "Breakpoint moved" },
            };
            return JsonSerializer.Serialize(new
            {
                seq = _sequence + 100,
                type = "response",
                request_seq = _sequence,
                success = true,
                command = "setBreakpoints",
                body = new { breakpoints },
            });
        }

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
