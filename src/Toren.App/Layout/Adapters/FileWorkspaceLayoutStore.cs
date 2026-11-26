using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Toren.App.Documents.Models;
using Toren.App.Layout.Contracts;
using Toren.App.Layout.Models;
using Toren.Core.Results;

namespace Toren.App.Layout.Adapters;

/// <summary>
/// Stores one small JSON file per workspace in <paramref name="directory"/>
/// (for example %AppData%/Toren/layouts), named after a hash of the workspace path.
/// </summary>
public sealed class FileWorkspaceLayoutStore(string directory) : IWorkspaceLayoutStore
{
    private readonly string _directory = !string.IsNullOrWhiteSpace(directory)
        ? directory
        : throw new ArgumentException("Layout directory is required.", nameof(directory));

    public async Task<Result<WorkspaceLayoutState>> LoadAsync(
        string workspacePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);

        var filePath = GetFilePath(workspacePath);
        if (!File.Exists(filePath))
        {
            return Result.Success(WorkspaceLayoutState.Default);
        }

        try
        {
            await using var stream = File.OpenRead(filePath);
            var stored = await JsonSerializer.DeserializeAsync<StoredLayout>(
                stream,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (stored is null
                || stored.Version != 1
                || !TryParseOrientation(stored.SplitOrientation, out var orientation)
                || !IsValidSize(stored.SidebarWidth)
                || !IsValidSize(stored.ToolPanelHeight)
                || !IsValidRatio(stored.SplitRatio))
            {
                return Result.Failure<WorkspaceLayoutState>(InvalidFormat());
            }

            return Result.Success(new WorkspaceLayoutState(
                stored.SidebarWidth,
                stored.ToolPanelHeight,
                orientation,
                stored.SplitRatio,
                stored.SecondaryDocumentPath,
                stored.PinnedDocumentPaths ?? [],
                stored.SyncWithActiveDocument ?? true));
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or SecurityException or JsonException)
        {
            return Result.Failure<WorkspaceLayoutState>(
                exception is JsonException
                    ? InvalidFormat()
                    : OperationError.Create("layout.read-failed", $"Could not read the workspace layout: {exception.Message}"));
        }
    }

    public async Task<Result<WorkspaceLayoutState>> SaveAsync(
        string workspacePath,
        WorkspaceLayoutState layout,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        ArgumentNullException.ThrowIfNull(layout);

        if (!IsValidSize(layout.SidebarWidth)
            || !IsValidSize(layout.ToolPanelHeight)
            || !IsValidRatio(layout.SplitRatio))
        {
            return Result.Failure<WorkspaceLayoutState>(InvalidFormat());
        }

        var filePath = GetFilePath(workspacePath);
        var temporaryPath = $"{filePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            Directory.CreateDirectory(_directory);
            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    new StoredLayout(
                        Version: 1,
                        WorkspacePath: Path.GetFullPath(workspacePath),
                        SidebarWidth: layout.SidebarWidth,
                        ToolPanelHeight: layout.ToolPanelHeight,
                        SplitOrientation: layout.SplitOrientation.ToString().ToLowerInvariant(),
                        SplitRatio: layout.SplitRatio,
                        SecondaryDocumentPath: layout.SecondaryDocumentPath,
                        PinnedDocumentPaths: layout.PinnedDocumentPaths ?? [],
                        SyncWithActiveDocument: layout.SyncWithActiveDocument),
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, filePath, overwrite: true);
            return Result.Success(layout);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            return Result.Failure<WorkspaceLayoutState>(
                OperationError.Create("layout.write-failed", $"Could not save the workspace layout: {exception.Message}"));
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

    private string GetFilePath(string workspacePath)
    {
        var normalized = Path.GetFullPath(workspacePath);
        if (!OperatingSystem.IsLinux())
        {
            normalized = normalized.ToUpperInvariant();
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
        return Path.Combine(_directory, $"{hash[..16].ToLowerInvariant()}.json");
    }

    private static OperationError InvalidFormat() =>
        OperationError.Create("layout.invalid-format", "The stored workspace layout is not valid and was ignored.");

    private static bool IsValidSize(double? value) =>
        value is null or (> 0d and < 20000d);

    private static bool IsValidRatio(double? value) =>
        value is null or (>= 0d and <= 1d);

    private static bool TryParseOrientation(string? value, out EditorSplitOrientation orientation)
    {
        switch (value?.ToLowerInvariant())
        {
            case null:
            case "none":
                orientation = EditorSplitOrientation.None;
                return true;
            case "horizontal":
                orientation = EditorSplitOrientation.Horizontal;
                return true;
            case "vertical":
                orientation = EditorSplitOrientation.Vertical;
                return true;
            default:
                orientation = default;
                return false;
        }
    }

}
