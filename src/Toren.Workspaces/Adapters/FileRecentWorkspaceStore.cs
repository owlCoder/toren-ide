using System.Security;
using System.Text.Json;
using Toren.Core.Results;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Errors;
using Toren.Workspaces.Models;

namespace Toren.Workspaces.Adapters;

public sealed class FileRecentWorkspaceStore(string filePath) : IRecentWorkspaceStore
{
    private const int MaxRecentWorkspaces = 10;
    private readonly string _filePath = !string.IsNullOrWhiteSpace(filePath)
        ? filePath
        : throw new ArgumentException("History path is required.", nameof(filePath));

    public async Task<Result<IReadOnlyList<WorkspaceDescriptor>>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return Result.Success<IReadOnlyList<WorkspaceDescriptor>>([]);
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            var history = await JsonSerializer.DeserializeAsync<RecentWorkspaceHistory>(
                stream,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (history is null || history.Version != 1 || history.Workspaces is null)
            {
                return Result.Failure<IReadOnlyList<WorkspaceDescriptor>>(
                    RecentWorkspaceErrors.InvalidFormat());
            }

            if (history.Workspaces.Any(workspace =>
                workspace is null || string.IsNullOrWhiteSpace(workspace.Path)
                || !Path.IsPathFullyQualified(workspace.Path)
                || !Enum.IsDefined(workspace.Kind)))
            {
                return Result.Failure<IReadOnlyList<WorkspaceDescriptor>>(
                    RecentWorkspaceErrors.InvalidFormat());
            }

            return Result.Success<IReadOnlyList<WorkspaceDescriptor>>(history.Workspaces.Take(MaxRecentWorkspaces).ToArray());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException or JsonException)
        {
            return Result.Failure<IReadOnlyList<WorkspaceDescriptor>>(
                exception is JsonException
                    ? RecentWorkspaceErrors.InvalidFormat()
                    : RecentWorkspaceErrors.ReadFailed(exception.Message));
        }
    }

    public async Task<Result<IReadOnlyList<WorkspaceDescriptor>>> RecordAsync(
        WorkspaceDescriptor workspace,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        var loaded = await LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!loaded.IsSuccess && loaded.Error.Code != "workspace.history.invalid")
        {
            return loaded;
        }

        var recent = (loaded.IsSuccess ? loaded.Value : Array.Empty<WorkspaceDescriptor>())
            .Where(item => !item.Path.Equals(workspace.Path, StringComparison.OrdinalIgnoreCase))
            .Prepend(workspace)
            .Take(MaxRecentWorkspaces)
            .ToArray();

        var directory = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException("History file has no directory.");
        var temporaryPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            Directory.CreateDirectory(directory);
            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    new RecentWorkspaceHistory(1, recent),
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, _filePath, overwrite: true);
            return Result.Success<IReadOnlyList<WorkspaceDescriptor>>(recent);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            return Result.Failure<IReadOnlyList<WorkspaceDescriptor>>(
                RecentWorkspaceErrors.WriteFailed(exception.Message));
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
                // A failed cleanup must not hide the original result.
            }
        }
    }

    private sealed record RecentWorkspaceHistory(int Version, WorkspaceDescriptor[] Workspaces);
}
