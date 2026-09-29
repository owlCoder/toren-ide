using System.Text.Json;
using Toren.Core.Results;
using Toren.DotNet.Execution.Contracts;
using Toren.DotNet.Execution.Errors;
using Toren.DotNet.Execution.Models;

namespace Toren.DotNet.Execution.Adapters;

public sealed class FileDotNetLaunchProfileProvider : IDotNetLaunchProfileProvider
{
    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public async Task<Result<IReadOnlyList<DotNetLaunchProfile>>> GetProfilesAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        cancellationToken.ThrowIfCancellationRequested();

        var fullProjectPath = Path.GetFullPath(projectPath);
        var projectDirectory = Path.GetDirectoryName(fullProjectPath);
        if (string.IsNullOrWhiteSpace(projectDirectory))
        {
            return Result.Success<IReadOnlyList<DotNetLaunchProfile>>([]);
        }

        var launchSettingsPath = Path.Combine(projectDirectory, "Properties", "launchSettings.json");
        if (!File.Exists(launchSettingsPath))
        {
            return Result.Success<IReadOnlyList<DotNetLaunchProfile>>([]);
        }

        try
        {
            await using var stream = File.OpenRead(launchSettingsPath);
            using var document = await JsonDocument
                .ParseAsync(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            return Result.Success<IReadOnlyList<DotNetLaunchProfile>>(
                ParseProfiles(document.RootElement));
        }
        catch (JsonException exception)
        {
            return Result.Failure<IReadOnlyList<DotNetLaunchProfile>>(
                DotNetLaunchProfileErrors.ParseFailed(launchSettingsPath, exception.Message));
        }
        catch (IOException exception)
        {
            return Result.Failure<IReadOnlyList<DotNetLaunchProfile>>(
                DotNetLaunchProfileErrors.ReadFailed(launchSettingsPath, exception.Message));
        }
        catch (UnauthorizedAccessException exception)
        {
            return Result.Failure<IReadOnlyList<DotNetLaunchProfile>>(
                DotNetLaunchProfileErrors.ReadFailed(launchSettingsPath, exception.Message));
        }
    }

    private static List<DotNetLaunchProfile> ParseProfiles(JsonElement root)
    {
        if (!root.TryGetProperty("profiles", out var profiles)
            || profiles.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var result = new List<DotNetLaunchProfile>();
        foreach (var property in profiles.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object
                || !TryGetString(property.Value, "commandName", out var commandName)
                || !commandName.Equals("Project", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(new DotNetLaunchProfile(
                property.Name,
                TryGetBoolean(property.Value, "launchBrowser"),
                GetOptionalString(property.Value, "launchUrl"),
                GetOptionalString(property.Value, "applicationUrl"),
                GetEnvironmentVariables(property.Value)));
        }

        return result;
    }

    private static Dictionary<string, string> GetEnvironmentVariables(JsonElement profile)
    {
        if (!profile.TryGetProperty("environmentVariables", out var environmentVariables)
            || environmentVariables.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in environmentVariables.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String
                && property.Value.GetString() is { } value)
            {
                result[property.Name] = value;
            }
        }

        return result;
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string value)
    {
        if (element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            && property.GetString() is { Length: > 0 } stringValue)
        {
            value = stringValue;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static string? GetOptionalString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static bool TryGetBoolean(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property)
        && property.ValueKind is JsonValueKind.True or JsonValueKind.False
        && property.GetBoolean();
}
