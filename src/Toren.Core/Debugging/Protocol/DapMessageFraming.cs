using System.Globalization;
using System.Text;
using Toren.Core.Results;

namespace Toren.Core.Debugging.Protocol;

public static class DapMessageFraming
{
    private const string ContentLengthHeader = "Content-Length:";
    private const string InvalidHeaderErrorCode = "debug.dap.frame.header-invalid";
    private const string InvalidPayloadErrorCode = "debug.dap.frame.payload-invalid";
    private static readonly byte[] HeaderSeparator = "\r\n\r\n"u8.ToArray();
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static byte[] Encode(string payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var headerBytes = Encoding.ASCII.GetBytes(
            $"Content-Length: {payloadBytes.Length.ToString(CultureInfo.InvariantCulture)}\r\n\r\n");
        var frame = new byte[headerBytes.Length + payloadBytes.Length];
        headerBytes.CopyTo(frame, 0);
        payloadBytes.CopyTo(frame, headerBytes.Length);
        return frame;
    }

    public static Result<DapProtocolFrame> TryRead(ReadOnlyMemory<byte> buffer)
    {
        var span = buffer.Span;
        var headerEnd = span.IndexOf(HeaderSeparator);
        if (headerEnd < 0)
        {
            return Result.Success(DapProtocolFrame.Incomplete);
        }

        var headerText = Encoding.ASCII.GetString(span[..headerEnd]);
        var contentLength = ParseContentLength(headerText);
        if (contentLength is null)
        {
            return Result.Failure<DapProtocolFrame>(
                OperationError.Create(
                    InvalidHeaderErrorCode,
                    "The DAP frame does not contain one valid Content-Length header."));
        }

        var payloadStart = headerEnd + HeaderSeparator.Length;
        if (span.Length - payloadStart < contentLength.Value)
        {
            return Result.Success(DapProtocolFrame.Incomplete);
        }

        try
        {
            var payload = StrictUtf8.GetString(span.Slice(payloadStart, contentLength.Value));
            return Result.Success(DapProtocolFrame.Complete(payload, payloadStart + contentLength.Value));
        }
        catch (DecoderFallbackException exception)
        {
            return Result.Failure<DapProtocolFrame>(
                OperationError.Create(
                    InvalidPayloadErrorCode,
                    $"The DAP frame payload is not valid UTF-8: {exception.Message}"));
        }
    }

    private static int? ParseContentLength(string headerText)
    {
        int? contentLength = null;
        foreach (var line in headerText.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
        {
            if (!line.StartsWith(ContentLengthHeader, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (contentLength is not null)
            {
                return null;
            }

            var value = line[ContentLengthHeader.Length..].Trim();
            if (!int.TryParse(
                    value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var parsedLength)
                || parsedLength < 0)
            {
                return null;
            }

            contentLength = parsedLength;
        }

        return contentLength;
    }
}
