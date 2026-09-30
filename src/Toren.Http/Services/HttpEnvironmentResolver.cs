using System.Text;
using Toren.Core.Results;
using Toren.Http.Contracts;
using Toren.Http.Models;

namespace Toren.Http.Services;

public sealed class HttpEnvironmentResolver : IHttpEnvironmentResolver
{
    private const string MissingVariableCode = "http.environment.variable-missing";

    public Result<HttpRequestDefinition> Resolve(
        HttpRequestDefinition request,
        IReadOnlyDictionary<string, string> fileVariables,
        IReadOnlyDictionary<string, string>? environmentVariables = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(fileVariables);

        var variables = new Dictionary<string, string>(fileVariables, StringComparer.OrdinalIgnoreCase);
        if (environmentVariables is not null)
        {
            foreach (var pair in environmentVariables)
            {
                variables[pair.Key] = pair.Value;
            }
        }

        var url = ResolveText(request.Url, variables);
        if (url.IsFailure)
        {
            return Result.Failure<HttpRequestDefinition>(url.Error);
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in request.Headers)
        {
            var value = ResolveText(pair.Value, variables);
            if (value.IsFailure)
            {
                return Result.Failure<HttpRequestDefinition>(value.Error);
            }

            headers[pair.Key] = value.Value!;
        }

        string? body = null;
        if (request.Body is not null)
        {
            var resolvedBody = ResolveText(request.Body, variables);
            if (resolvedBody.IsFailure)
            {
                return Result.Failure<HttpRequestDefinition>(resolvedBody.Error);
            }

            body = resolvedBody.Value;
        }

        return Result.Success(request with
        {
            Url = url.Value!,
            Headers = headers,
            Body = body,
        });
    }

    private static Result<string> ResolveText(
        string text,
        IReadOnlyDictionary<string, string> variables)
    {
        var output = new StringBuilder(text.Length);
        var position = 0;
        while (position < text.Length)
        {
            var start = text.IndexOf("{{", position, StringComparison.Ordinal);
            if (start < 0)
            {
                output.Append(text, position, text.Length - position);
                break;
            }

            output.Append(text, position, start - position);
            var end = text.IndexOf("}}", start + 2, StringComparison.Ordinal);
            if (end < 0)
            {
                output.Append(text, start, text.Length - start);
                break;
            }

            var key = text[(start + 2)..end].Trim();
            if (key.Length == 0 || !variables.TryGetValue(key, out var value))
            {
                return Result.Failure<string>(new OperationError(
                    MissingVariableCode,
                    $"HTTP variable '{key}' is not defined."));
            }

            output.Append(value);
            position = end + 2;
        }

        return Result.Success(output.ToString());
    }
}
