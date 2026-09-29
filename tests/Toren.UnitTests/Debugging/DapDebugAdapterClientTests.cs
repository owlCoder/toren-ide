using System.Text;
using NUnit.Framework;
using Toren.Debugging.Contracts;
using Toren.Debugging.Protocol;
using Toren.Debugging.Services;

namespace Toren.UnitTests.Debugging;

[TestFixture]
public sealed class DapDebugAdapterClientTests
{
    [Test]
    public async Task SendRequestWritesFrameAndReturnsCorrelatedResponse()
    {
        const string response = """
            {"seq":2,"type":"response","request_seq":1,"success":true,"command":"threads","body":{"threads":[]}}
            """;
        var transport = new MemoryDebugAdapterTransport(response);
        await using var client = new DapDebugAdapterClient(transport);

        var result = await client.SendRequestAsync("threads");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value!.Command, Is.EqualTo("threads"));
            Assert.That(result.Value.RequestSequence, Is.EqualTo(1));
            var written = Encoding.UTF8.GetString(transport.WrittenBytes);
            Assert.That(written, Does.Contain("Content-Length:"));
            Assert.That(written, Does.Contain("\"type\":\"request\""));
            Assert.That(written, Does.Contain("\"command\":\"threads\""));
        });
    }

    [Test]
    public async Task FailedResponseReturnsAdapterFailure()
    {
        const string response = """
            {"seq":2,"type":"response","request_seq":1,"success":false,"command":"launch","message":"program was not found"}
            """;
        var transport = new MemoryDebugAdapterTransport(response);
        await using var client = new DapDebugAdapterClient(transport);

        var result = await client.SendRequestAsync("launch", new { program = "missing.dll" });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("debug.dap.response.failed"));
            Assert.That(result.Error.Message, Does.Contain("program was not found"));
        });
    }

    [Test]
    public async Task ReadNotificationsYieldsAdapterEvents()
    {
        const string eventPayload = """
            {"seq":7,"type":"event","event":"initialized","body":{}}
            """;
        var transport = new MemoryDebugAdapterTransport(eventPayload);
        await using var client = new DapDebugAdapterClient(transport);
        var notifications = new List<DapProtocolMessage>();

        await foreach (var notification in client.ReadNotificationsAsync())
        {
            notifications.Add(notification);
        }

        Assert.Multiple(() =>
        {
            Assert.That(notifications, Has.Count.EqualTo(1));
            Assert.That(notifications[0].Kind, Is.EqualTo(DapProtocolMessageKind.Event));
            Assert.That(notifications[0].EventName, Is.EqualTo("initialized"));
        });
    }

    [Test]
    public async Task TerminateForwardsToTransport()
    {
        var transport = new MemoryDebugAdapterTransport();
        await using var client = new DapDebugAdapterClient(transport);

        client.Terminate();

        Assert.That(transport.IsTerminated, Is.True);
    }

    private sealed class MemoryDebugAdapterTransport : IDebugAdapterTransport
    {
        private readonly MemoryStream _readStream;
        private readonly MemoryStream _writeStream = new();

        public MemoryDebugAdapterTransport(string? inboundPayload = null)
        {
            _readStream = inboundPayload is null
                ? new MemoryStream()
                : new MemoryStream(DapFrameCodec.Encode(inboundPayload), writable: false);
        }

        public Stream ReadStream => _readStream;

        public Stream WriteStream => _writeStream;

        public TextReader ErrorReader => TextReader.Null;

        public byte[] WrittenBytes => _writeStream.ToArray();

        public bool IsTerminated { get; private set; }

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
            _readStream.Dispose();
            _writeStream.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
