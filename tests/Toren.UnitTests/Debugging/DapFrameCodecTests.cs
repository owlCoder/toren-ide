using System.Text;
using NUnit.Framework;
using Toren.Debugging.Protocol;

namespace Toren.UnitTests.Debugging;

[TestFixture]
public sealed class DapFrameCodecTests
{
    [Test]
    public void EncodeUsesUtf8ByteLength()
    {
        const string payload = "{\"message\":\"ž\"}";
        var frame = DapFrameCodec.Encode(payload);
        var text = Encoding.UTF8.GetString(frame);
        Assert.That(text, Does.StartWith($"Content-Length: {Encoding.UTF8.GetByteCount(payload)}\r\n\r\n"));
        Assert.That(text, Does.EndWith(payload));
    }

    [Test]
    public void TryReadReturnsIncompleteUntilWholePayloadArrives()
    {
        var frame = DapFrameCodec.Encode("{\"seq\":1}");
        var result = DapFrameCodec.TryRead(frame.AsMemory(0, frame.Length - 1));
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.EqualTo(DapProtocolFrame.Incomplete));
    }

    [Test]
    public void TryReadConsumesOnlyFirstFrame()
    {
        var first = DapFrameCodec.Encode("{\"seq\":1}");
        var second = DapFrameCodec.Encode("{\"seq\":2}");
        var combined = new byte[first.Length + second.Length];
        first.CopyTo(combined, 0);
        second.CopyTo(combined, first.Length);
        var result = DapFrameCodec.TryRead(combined);
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.Payload, Is.EqualTo("{\"seq\":1}"));
        Assert.That(result.Value.BytesConsumed, Is.EqualTo(first.Length));
    }

    [Test]
    public void TryReadRejectsMissingContentLength()
    {
        var bytes = Encoding.ASCII.GetBytes("Content-Type: application/json\r\n\r\n{}");
        var result = DapFrameCodec.TryRead(bytes);
        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error.Code, Is.EqualTo("debug.dap.frame.header-invalid"));
    }

    [Test]
    public void TryReadRejectsInvalidUtf8Payload()
    {
        var header = Encoding.ASCII.GetBytes("Content-Length: 1\r\n\r\n");
        var bytes = new byte[header.Length + 1];
        header.CopyTo(bytes, 0);
        bytes[^1] = 0xff;
        var result = DapFrameCodec.TryRead(bytes);
        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error.Code, Is.EqualTo("debug.dap.frame.payload-invalid"));
    }
}
