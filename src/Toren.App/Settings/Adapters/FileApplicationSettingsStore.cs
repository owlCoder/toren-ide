using System.Security;
using System.Text.Json;
using Toren.App.Settings.Contracts;
using Toren.App.Settings.Errors;
using Toren.App.Settings.Models;
using Toren.Core.Results;

namespace Toren.App.Settings.Adapters;

public sealed class FileApplicationSettingsStore(string filePath) : IApplicationSettingsStore
{
    private const double MinimumEditorFontSize = 8d;
    private const double MaximumEditorFontSize = 40d;

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
                || !TryParseTheme(stored.Theme, out var theme)
                || !IsValidEditorFontSize(stored.EditorFontSize))
            {
                return Result.Failure<ApplicationSettings>(ApplicationSettingsErrors.InvalidFormat());
            }

            return Result.Success(new ApplicationSettings(
                theme,
                stored.EditorFontSize ?? ApplicationSettings.DefaultEditorFontSize,
                stored.ShowLineNumbers ?? ApplicationSettings.DefaultShowLineNumbers,
                stored.WordWrap ?? ApplicationSettings.DefaultWordWrap));
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

        var current = await LoadAsync(cancellationToken).ConfigureAwait(false);
        var baseline = current.IsSuccess ? current.Value! : ApplicationSettings.Default;
        var merged = new ApplicationSettings(
            settings.Theme,
            settings.EditorFontSize ?? baseline.EffectiveEditorFontSize,
            settings.ShowLineNumbers ?? baseline.EffectiveShowLineNumbers,
            settings.WordWrap ?? baseline.EffectiveWordWrap);
        if (!IsValidEditorFontSize(merged.EditorFontSize))
        {
            return Result.Failure<ApplicationSettings>(ApplicationSettingsErrors.InvalidFormat());
        }

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
                        Theme: merged.Theme == ApplicationThemePreference.Light ? "light" : "dark",
                        EditorFontSize: merged.EffectiveEditorFontSize,
                        ShowLineNumbers: merged.EffectiveShowLineNumbers,
                        WordWrap: merged.EffectiveWordWrap),
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, _filePath, overwrite: true);
            return Result.Success(merged);
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

    private static bool IsValidEditorFontSize(double? value) =>
        value is null or >= MinimumEditorFontSize and <= MaximumEditorFontSize;

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

    private sealed record StoredApplicationSettings(
        int Version,
        string Theme,
        double? EditorFontSize = null,
        bool? ShowLineNumbers = null,
        bool? WordWrap = null);
}
