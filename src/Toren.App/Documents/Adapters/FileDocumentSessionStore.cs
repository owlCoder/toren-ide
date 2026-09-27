using System.Security;
using System.Text.Json;
using Toren.App.Documents.Contracts;
using Toren.App.Documents.Errors;
using Toren.App.Documents.Models;
using Toren.Core.Results;

namespace Toren.App.Documents.Adapters;

public sealed class FileDocumentSessionStore(string filePath) : IDocumentSessionStore
{
    private const int MaxOpenDocuments = 50;
    private readonly string _filePath = !string.IsNullOrWhiteSpace(filePath)
        ? filePath
        : throw new ArgumentException("Session path is required.", nameof(filePath));

    public async Task<Result<DocumentSessionState>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return Result.Success(new DocumentSessionState([], null));
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            var stored = await JsonSerializer.DeserializeAsync<StoredDocumentSession>(
                stream,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (stored is null || stored.Version != 1 || stored.OpenDocumentPaths is null)
            {
                return Result.Failure<DocumentSessionState>(DocumentSessionErrors.InvalidFormat());
            }

            var paths = stored.OpenDocumentPaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Take(MaxOpenDocuments)
                .ToArray();
            if (paths.Length != stored.OpenDocumentPaths.Length
                || paths.Any(path => !Path.IsPathFullyQualified(path))
                || (stored.ActiveDocumentPath is not null
                    && (!Path.IsPathFullyQualified(stored.ActiveDocumentPath)
                        || !paths.Contains(stored.ActiveDocumentPath, StringComparer.Ordinal))))
            {
                return Result.Failure<DocumentSessionState>(DocumentSessionErrors.InvalidFormat());
            }

            return Result.Success(new DocumentSessionState(paths, stored.ActiveDocumentPath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException or JsonException)
        {
            return Result.Failure<DocumentSessionState>(
                exception is JsonException
                    ? DocumentSessionErrors.InvalidFormat()
                    : DocumentSessionErrors.ReadFailed(exception.Message));
        }
    }

    public async Task<Result<DocumentSessionState>> SaveAsync(
        DocumentSessionState session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(session.OpenDocumentPaths);

        var paths = session.OpenDocumentPaths
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.Ordinal)
            .Take(MaxOpenDocuments)
            .ToArray();
        var activePath = session.ActiveDocumentPath is null
            ? null
            : Path.GetFullPath(session.ActiveDocumentPath);
        if (activePath is not null && !paths.Contains(activePath, StringComparer.Ordinal))
        {
            activePath = null;
        }

        var normalized = new DocumentSessionState(paths, activePath);
        var directory = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException("Session file has no directory.");
        var temporaryPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            Directory.CreateDirectory(directory);
            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    new StoredDocumentSession(1, paths, activePath),
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, _filePath, overwrite: true);
            return Result.Success(normalized);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            return Result.Failure<DocumentSessionState>(DocumentSessionErrors.WriteFailed(exception.Message));
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
            {
                // Cleanup failure must not hide the original result.
            }
        }
    }

    private sealed record StoredDocumentSession(
        int Version,
        string[] OpenDocumentPaths,
        string? ActiveDocumentPath);
}
