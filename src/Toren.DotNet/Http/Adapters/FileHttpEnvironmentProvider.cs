using System.Text.Json;
using Toren.Core.Results;
using Toren.DotNet.Http.Contracts;
using Toren.DotNet.Http.Errors;
using Toren.DotNet.Http.Models;

namespace Toren.DotNet.Http.Adapters;

public sealed class FileHttpEnvironmentProvider : IHttpEnvironmentProvider
{
    private const string EnvironmentFileName = "http-client.env.json";
    private const string UserEnvironmentFileName = "http-client.env.json.user";
    private const string SharedEnvironmentName = "$shared";

    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public async Task<Result<IReadOnlyList<HttpEnvironment>>> GetEnvironmentsAsync(
        string documentPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);
        cancellationToken.ThrowIfCancellationRequested();

        var fullDocumentPath = Path.GetFullPath(documentPath);
        var directory = Path.GetDirectoryName(fullDocumentPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return Result.Success<IReadOnlyList<HttpEnvironment>>([]);
        }

        var environmentPath = FindEnvironmentPath(directory);
        if (environmentPath is null)
        {
            return Result.Success<IReadOnlyList<HttpEnvironment>>([]);
        }

        var mainResult = await ReadEnvironmentFileAsync(environmentPath, cancellationToken)
            .ConfigureAwait(false);
        if (mainResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<HttpEnvironment>>(mainResult.Error);
        }

        Dictionary<string, Dictionary<string, string>> userEnvironments =
            new(StringComparer.OrdinalIgnoreCase);
        var userPath = Path.Combine(Path.GetDirectoryName(environmentPath)!, UserEnvironmentFileName);
        if (File.Exists(userPath))
        {
            var userResult = await ReadEnvironmentFileAsync(userPath, cancellationToken)
                .ConfigureAwait(false);
            if (userResult.IsFailure)
            {
                return Result.Failure<IReadOnlyList<HttpEnvironment>>(userResult.Error);
            }

            userEnvironments = userResult.Value!;
        }

        var mainEnvironments = mainResult.Value!;
        mainEnvironments.TryGetValue(SharedEnvironmentName, out var shared);
        userEnvironments.TryGetValue(SharedEnvironmentName, out var userShared);
        var names = mainEnvironments.Keys
            .Concat(userEnvironments.Keys)
            .Where(static name => !name.Equals(SharedEnvironmentName, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase);

        var environments = new List<HttpEnvironment>();
        foreach (var name in names)
        {
            var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Merge(variables, shared);
            if (mainEnvironments.TryGetValue(name, out var mainVariables))
            {
                Merge(variables, mainVariables);
            }

            Merge(variables, userShared);
            if (userEnvironments.TryGetValue(name, out var userVariables))
            {
                Merge(variables, userVariables);
            }

            environments.Add(new HttpEnvironment(name, variables));
        }

        return Result.Success<IReadOnlyList<HttpEnvironment>>(environments);
    }

    private static string? FindEnvironmentPath(string startDirectory)
    {
        for (var directory = new DirectoryInfo(startDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, EnvironmentFileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static async Task<Result<Dictionary<string, Dictionary<string, string>>>> ReadEnvironmentFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            using var document = await JsonDocument.ParseAsync(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Result.Failure<Dictionary<string, Dictionary<string, string>>>(
                    HttpEnvironmentErrors.ParseFailed(path, "The root value must be a JSON object."));
            }

            var environments = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var environmentProperty in document.RootElement.EnumerateObject())
            {
                if (environmentProperty.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                environments[environmentProperty.Name] = ReadVariables(environmentProperty.Value);
            }

            return Result.Success(environments);
        }
        catch (JsonException exception)
        {
            return Result.Failure<Dictionary<string, Dictionary<string, string>>>(
                HttpEnvironmentErrors.ParseFailed(path, exception.Message));
        }
        catch (IOException exception)
        {
            return Result.Failure<Dictionary<string, Dictionary<string, string>>>(
                HttpEnvironmentErrors.ReadFailed(path, exception.Message));
        }
        catch (UnauthorizedAccessException exception)
        {
            return Result.Failure<Dictionary<string, Dictionary<string, string>>>(
                HttpEnvironmentErrors.ReadFailed(path, exception.Message));
        }
    }

    private static Dictionary<string, string> ReadVariables(JsonElement environment)
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in environment.EnumerateObject())
        {
            var value = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => property.Value.GetRawText(),
                _ => null,
            };
            if (value is not null)
            {
                variables[property.Name] = value;
            }
        }

        return variables;
    }

    private static void Merge(
        Dictionary<string, string> target,
        Dictionary<string, string>? source)
    {
        if (source is null)
        {
            return;
        }

        foreach (var item in source)
        {
            target[item.Key] = item.Value;
        }
    }
}
