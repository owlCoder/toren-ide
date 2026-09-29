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
public sealed class DapDebugInspectionTests
{
    [Test]
    public async Task InspectionRequestsMapAndParseDapResponses()
    {
        var client = new InspectionClient();
        var service = new DapDebugSessionService(
            new SuccessfulLocator(),
            new TransportFactory(),
            new ClientFactory(client));
        var attached = await service.AttachAsync(42);
        Assert.That(attached.IsSuccess, Is.True);
        await using var session = attached.Value!;

        var stack = await session.GetStackTraceAsync(7);
        var scopes = await session.GetScopesAsync(101);
        var variables = await session.GetVariablesAsync(200);
        var evaluation = await session.EvaluateAsync(
            "count + 1",
            101,
            DebugEvaluationContext.Watch);

        Assert.Multiple(() =>
        {
            Assert.That(stack.IsSuccess, Is.True);
            Assert.That(stack.Value, Has.Count.EqualTo(1));
            Assert.That(stack.Value![0], Is.EqualTo(new DebugStackFrame(
                101,
                "Program.Main()",
                "/workspace/Program.cs",
                12,
                9)));
            Assert.That(scopes.IsSuccess, Is.True);
            Assert.That(scopes.Value, Has.Count.EqualTo(1));
            Assert.That(scopes.Value![0], Is.EqualTo(new DebugScope("Locals", 200, false)));
            Assert.That(variables.IsSuccess, Is.True);
            Assert.That(variables.Value, Has.Count.EqualTo(1));
            Assert.That(variables.Value![0], Is.EqualTo(new DebugVariable("count", "5", "int", 0)));
            Assert.That(evaluation.IsSuccess, Is.True);
            Assert.That(evaluation.Value, Is.EqualTo(new DebugEvaluationResult("6", "int", 0)));
            Assert.That(client.Arguments["stackTrace"], Does.Contain("\"threadId\":7"));
            Assert.That(client.Arguments["scopes"], Does.Contain("\"frameId\":101"));
            Assert.That(client.Arguments["variables"], Does.Contain("\"variablesReference\":200"));
            Assert.That(client.Arguments["evaluate"], Does.Contain("\"context\":\"watch\""));
        });
    }

    [Test]
    public async Task InvalidInspectionPayloadReturnsRecoverableFailure()
    {
        var client = new InspectionClient(invalidVariables: true);
        var service = new DapDebugSessionService(
            new SuccessfulLocator(),
            new TransportFactory(),
            new ClientFactory(client));
        var attached = await service.AttachAsync(42);
        Assert.That(attached.IsSuccess, Is.True);
        await using var session = attached.Value!;

        var variables = await session.GetVariablesAsync(200);

        Assert.Multiple(() =>
        {
            Assert.That(variables.IsFailure, Is.True);
            Assert.That(variables.Error.Code, Is.EqualTo("debug.session.invalid-variables"));
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

    private sealed class ClientFactory(InspectionClient client) : IDebugAdapterClientFactory
    {
        public IDebugAdapterClient Create(IDebugAdapterTransport transport) => client;
    }

    private sealed class InspectionClient(bool invalidVariables = false) : IDebugAdapterClient
    {
        private int _sequence;

        public Dictionary<string, string> Arguments { get; } = new(StringComparer.Ordinal);

        public Task<Result<DapProtocolMessage>> SendRequestAsync(
            string command,
            object? arguments = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var requestSequence = Interlocked.Increment(ref _sequence);
            Arguments[command] = arguments is null ? string.Empty : JsonSerializer.Serialize(arguments);
            var payload = command switch
            {
                "stackTrace" => $$"""
                    {"seq":{{requestSequence + 100}},"type":"response","request_seq":{{requestSequence}},"success":true,"command":"stackTrace","body":{"stackFrames":[{"id":101,"name":"Program.Main()","source":{"path":"/workspace/Program.cs"},"line":12,"column":9}]}}
                    """,
                "scopes" => $$"""
                    {"seq":{{requestSequence + 100}},"type":"response","request_seq":{{requestSequence}},"success":true,"command":"scopes","body":{"scopes":[{"name":"Locals","variablesReference":200,"expensive":false}]}}
                    """,
                "variables" when invalidVariables => $$"""
                    {"seq":{{requestSequence + 100}},"type":"response","request_seq":{{requestSequence}},"success":true,"command":"variables","body":{"variables":[{"name":"count"}]}}
                    """,
                "variables" => $$"""
                    {"seq":{{requestSequence + 100}},"type":"response","request_seq":{{requestSequence}},"success":true,"command":"variables","body":{"variables":[{"name":"count","value":"5","type":"int","variablesReference":0}]}}
                    """,
                "evaluate" => $$"""
                    {"seq":{{requestSequence + 100}},"type":"response","request_seq":{{requestSequence}},"success":true,"command":"evaluate","body":{"result":"6","type":"int","variablesReference":0}}
                    """,
                _ => $$"""
                    {"seq":{{requestSequence + 100}},"type":"response","request_seq":{{requestSequence}},"success":true,"command":"{{command}}"}
                    """,
            };
            return Task.FromResult(Result.Success(new DapProtocolMessage(
                requestSequence + 100,
                DapProtocolMessageKind.Response,
                command,
                EventName: null,
                RequestSequence: requestSequence,
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
