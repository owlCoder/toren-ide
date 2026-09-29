using System.Buffers;
using System.Globalization;
using System.Text;

namespace Toren.Debugging.Services;

public sealed class DapMessageFramingService
{
    private static readonly byte[] HeaderTerminator = "\r\n\r\n"u8.ToArray();

    public async Task<string?> ReadPayloadAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var headerBytes = new ArrayBufferWriter<byte>();
        var terminatorMatch = 0;
        while (true)
        {
            var next = new byte[1];
            var read = await stream.ReadAsync(next, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return headerBytes.WrittenCount == 0
                    ? null
                    : throw new EndOfStreamException("DAP header ended before the header terminator.");
            }

            headerBytes.Write(next);
            terminatorMatch = next[0] == HeaderTerminator[terminatorMatch]
                ? terminatorMatch + 1
                : next[0] == HeaderTerminator[0] ? 1 : 0;
            if (terminatorMatch == HeaderTerminator.Length)
            {
                break;
            }

            if (headerBytes.WrittenCount > 16 * 1024)
            {
                throw new InvalidDataException("DAP header exceeded the supported 16 KiB limit.");
            }
        }

        var headerText = Encoding.ASCII.GetString(headerBytes.WrittenSpan);
        var contentLength = ParseContentLength(headerText);
        var payload = new byte[contentLength];
        var offset = 0;
        while (offset < payload.Length)
        {
            var read = await stream
                .ReadAsync(payload.AsMemory(offset), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("DAP payload ended before Content-Length bytes were received.");
            }

            offset += read;
        }

        return Encoding.UTF8.GetString(payload);
    }

    public async Task WritePayloadAsync(
        Stream stream,
        string payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(payload);

        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var header = Encoding.ASCII.GetBytes(
            $"Content-Length: {payloadBytes.Length.ToString(CultureInfo.InvariantCulture)}\r\n\r\n");
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payloadBytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static int ParseContentLength(string headers)
    {
        foreach (var line in headers.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
        {
            const string prefix = "Content-Length:";
            if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var rawValue = line[prefix.Length..].Trim();
            if (int.TryParse(
                    rawValue,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var contentLength)
                && contentLength >= 0)
            {
                return contentLength;
            }

            throw new InvalidDataException("DAP Content-Length header is invalid.");
        }

        throw new InvalidDataException("DAP message is missing a Content-Length header.");
    }
}
