using System.Security;
using System.Text.Json;
using Toren.App.Settings.Contracts;
using Toren.App.Settings.Errors;
using Toren.App.Settings.Models;
using Toren.Core.Results;

namespace Toren.App.Settings.Adapters;

public sealed class FileApplicationSettingsStore(string filePath) : IApplicationSettingsStore
{
    private readonly string _filePath = !string.IsNullOrWhiteSpace(filePath)
        ? filePath
        : throw new ArgumentException("Application settings path is required.", nameof(filePath));

    public async Task<Result<ApplicationSettings>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return Result.Success(ApplicationSettings.Default);
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            var stored = await JsonSerializer.DeserializeAsync<StoredApplicationSettings>(
                stream,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (stored is null
                || stored.Version != 1
                || !TryParseTheme(stored.Theme, out var theme))
            {
                return Result.Failure<ApplicationSettings>(ApplicationSettingsErrors.InvalidFormat());
            }

            return Result.Success(new ApplicationSettings(theme));
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or SecurityException or JsonException)
        {
            return Result.Failure<ApplicationSettings>(
                exception is JsonException
                    ? ApplicationSettingsErrors.InvalidFormat()
                    : ApplicationSettingsErrors.ReadFailed(exception.Message));
        }
    }

    public async Task<Result<ApplicationSettings>> SaveAsync(
        ApplicationSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var directory = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException("Application settings file has no directory.");
        var temporaryPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            Directory.CreateDirectory(directory);
            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    new StoredApplicationSettings(
                        Version: 1,
                        Theme: settings.Theme == ApplicationThemePreference.Light ? "light" : "dark"),
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, _filePath, overwrite: true);
            return Result.Success(settings);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            return Result.Failure<ApplicationSettings>(ApplicationSettingsErrors.WriteFailed(exception.Message));
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

    private static bool TryParseTheme(string? value, out ApplicationThemePreference theme)
    {
        if (string.Equals(value, "dark", StringComparison.OrdinalIgnoreCase))
        {
            theme = ApplicationThemePreference.Dark;
            return true;
        }

        if (string.Equals(value, "light", StringComparison.OrdinalIgnoreCase))
        {
            theme = ApplicationThemePreference.Light;
            return true;
        }

        theme = default;
        return false;
    }

    private sealed record StoredApplicationSettings(int Version, string Theme);
}
