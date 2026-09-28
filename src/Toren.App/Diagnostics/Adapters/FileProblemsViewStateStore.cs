using System.Security;
using System.Text.Json;
using Toren.App.Diagnostics.Contracts;
using Toren.App.Diagnostics.Errors;
using Toren.App.Diagnostics.Models;
using Toren.Core.Results;

namespace Toren.App.Diagnostics.Adapters;

public sealed class FileProblemsViewStateStore(string filePath) : IProblemsViewStateStore
{
    private readonly string _filePath = !string.IsNullOrWhiteSpace(filePath)
        ? filePath
        : throw new ArgumentException("Problems view-state path is required.", nameof(filePath));

    public async Task<Result<ProblemsViewState>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return Result.Success(ProblemsViewState.Default);
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            var stored = await JsonSerializer.DeserializeAsync<StoredProblemsViewState>(
                stream,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (stored is null || stored.Version != 1)
            {
                return Result.Failure<ProblemsViewState>(ProblemsViewStateErrors.InvalidFormat());
            }

            return Result.Success(new ProblemsViewState(
                stored.ShowErrors,
                stored.ShowWarnings,
                stored.ShowInfo));
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or SecurityException or JsonException)
        {
            return Result.Failure<ProblemsViewState>(
                exception is JsonException
                    ? ProblemsViewStateErrors.InvalidFormat()
                    : ProblemsViewStateErrors.ReadFailed(exception.Message));
        }
    }

    public async Task<Result<ProblemsViewState>> SaveAsync(
        ProblemsViewState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        var directory = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException("Problems view-state file has no directory.");
        var temporaryPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            Directory.CreateDirectory(directory);
            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    new StoredProblemsViewState(
                        Version: 1,
                        state.ShowErrors,
                        state.ShowWarnings,
                        state.ShowInfo),
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, _filePath, overwrite: true);
            return Result.Success(state);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            return Result.Failure<ProblemsViewState>(ProblemsViewStateErrors.WriteFailed(exception.Message));
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
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or SecurityException)
            {
                // Cleanup failure must not hide the original result.
            }
        }
    }

    private sealed record StoredProblemsViewState(
        int Version,
        bool ShowErrors,
        bool ShowWarnings,
        bool ShowInfo);
}
