using System.Security;
using System.Text;
using Toren.App.Documents.Contracts;
using Toren.App.Documents.Errors;
using Toren.App.Documents.Models;
using Toren.Core.Results;

namespace Toren.App.Documents.Adapters;

public sealed class FileTextDocumentStore : ITextDocumentStore
{
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];
    private static readonly byte[] Utf16LittleEndianBom = [0xFF, 0xFE];
    private static readonly byte[] Utf16BigEndianBom = [0xFE, 0xFF];
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly UnicodeEncoding Utf16LittleEndian = new(bigEndian: false, byteOrderMark: true, throwOnInvalidBytes: true);
    private static readonly UnicodeEncoding Utf16BigEndian = new(bigEndian: true, byteOrderMark: true, throwOnInvalidBytes: true);

    public async Task<Result<TextDocumentContent>> LoadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            return Result.Failure<TextDocumentContent>(TextDocumentErrors.NotFound(fullPath));
        }

        try
        {
            var bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken).ConfigureAwait(false);
            var decoded = Decode(fullPath, bytes);
            if (!decoded.IsSuccess)
            {
                return decoded;
            }

            if (decoded.Value.Text.Contains('\0'))
            {
                return Result.Failure<TextDocumentContent>(TextDocumentErrors.BinaryFile(fullPath));
            }

            return decoded;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            return Result.Failure<TextDocumentContent>(
                TextDocumentErrors.ReadFailed(fullPath, exception.Message));
        }
    }

    public async Task<Result<TextDocumentContent>> SaveAsync(
        TextDocumentContent document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = Path.GetFullPath(document.Path);
        try
        {
            var bytes = Encode(document.Text, document.Encoding);
            await File.WriteAllBytesAsync(fullPath, bytes, cancellationToken).ConfigureAwait(false);
            return Result.Success(document with { Path = fullPath });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            return Result.Failure<TextDocumentContent>(
                TextDocumentErrors.WriteFailed(fullPath, exception.Message));
        }
    }

    private static Result<TextDocumentContent> Decode(string path, byte[] bytes)
    {
        try
        {
            if (HasPrefix(bytes, Utf8Bom))
            {
                return Result.Success(new TextDocumentContent(
                    path,
                    StrictUtf8.GetString(bytes.AsSpan(Utf8Bom.Length)),
                    TextDocumentEncoding.Utf8Bom));
            }

            if (HasPrefix(bytes, Utf16LittleEndianBom))
            {
                return Result.Success(new TextDocumentContent(
                    path,
                    Utf16LittleEndian.GetString(bytes.AsSpan(Utf16LittleEndianBom.Length)),
                    TextDocumentEncoding.Utf16LittleEndian));
            }

            if (HasPrefix(bytes, Utf16BigEndianBom))
            {
                return Result.Success(new TextDocumentContent(
                    path,
                    Utf16BigEndian.GetString(bytes.AsSpan(Utf16BigEndianBom.Length)),
                    TextDocumentEncoding.Utf16BigEndian));
            }

            return Result.Success(new TextDocumentContent(
                path,
                StrictUtf8.GetString(bytes),
                TextDocumentEncoding.Utf8));
        }
        catch (DecoderFallbackException)
        {
            return Result.Failure<TextDocumentContent>(TextDocumentErrors.UnsupportedEncoding(path));
        }
    }

    private static byte[] Encode(string text, TextDocumentEncoding encoding)
    {
        ArgumentNullException.ThrowIfNull(text);

        return encoding switch
        {
            TextDocumentEncoding.Utf8 => StrictUtf8.GetBytes(text),
            TextDocumentEncoding.Utf8Bom => Combine(Utf8Bom, StrictUtf8.GetBytes(text)),
            TextDocumentEncoding.Utf16LittleEndian => Combine(Utf16LittleEndianBom, Utf16LittleEndian.GetBytes(text)),
            TextDocumentEncoding.Utf16BigEndian => Combine(Utf16BigEndianBom, Utf16BigEndian.GetBytes(text)),
            _ => throw new ArgumentOutOfRangeException(nameof(encoding)),
        };
    }

    private static bool HasPrefix(byte[] bytes, byte[] prefix) =>
        bytes.Length >= prefix.Length && bytes.AsSpan(0, prefix.Length).SequenceEqual(prefix);

    private static byte[] Combine(byte[] prefix, byte[] content)
    {
        var combined = new byte[prefix.Length + content.Length];
        prefix.CopyTo(combined, 0);
        content.CopyTo(combined, prefix.Length);
        return combined;
    }
}
