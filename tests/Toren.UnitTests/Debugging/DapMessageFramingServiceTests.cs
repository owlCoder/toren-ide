using System.Text;
using NUnit.Framework;
using Toren.Debugging.Services;

namespace Toren.UnitTests.Debugging;

[TestFixture]
public sealed class DapMessageFramingServiceTests
{
    [Test]
    public async Task WritePayloadUsesUtf8ByteLength()
    {
        await using var stream = new MemoryStream();
        const string payload = "{\"message\":\"Здраво\"}";

        await DapMessageFramingService.WritePayloadAsync(stream, payload);

        var bytes = stream.ToArray();
        var text = Encoding.UTF8.GetString(bytes);
        var expectedLength = Encoding.UTF8.GetByteCount(payload);
        Assert.That(text, Is.EqualTo($"Content-Length: {expectedLength}\r\n\r\n{payload}"));
    }

    [Test]
    public async Task ReadPayloadHandlesChunkedStreamAndUtf8Content()
    {
        const string payload = "{\"event\":\"stopped\",\"description\":\"пауза\"}";
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var message = Encoding.UTF8.GetBytes($"Content-Length: {payloadBytes.Length}\r\n\r\n{payload}");
        await using var stream = new ChunkedReadStream(message, 3);

        var result = await DapMessageFramingService.ReadPayloadAsync(stream);

        Assert.That(result, Is.EqualTo(payload));
    }

    [Test]
    public async Task ReadPayloadReturnsNullAtCleanEndOfStream()
    {
        await using var stream = new MemoryStream();

        Assert.That(await DapMessageFramingService.ReadPayloadAsync(stream), Is.Null);
    }

    [Test]
    public void ReadPayloadRejectsMissingContentLength()
    {
        var bytes = Encoding.ASCII.GetBytes("X-Test: value\r\n\r\n{}");
        using var stream = new MemoryStream(bytes);

        Assert.That(
            async () => await DapMessageFramingService.ReadPayloadAsync(stream),
            Throws.TypeOf<InvalidDataException>());
    }

    private sealed class ChunkedReadStream(byte[] buffer, int maximumReadSize) : MemoryStream(buffer)
    {
        public override ValueTask<int> ReadAsync(
            Memory<byte> destination,
            CancellationToken cancellationToken = default) =>
            base.ReadAsync(destination[..Math.Min(destination.Length, maximumReadSize)], cancellationToken);
    }
}
