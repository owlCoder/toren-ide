using System.Text;
using Toren.Core.Results;
using Toren.DotNet.Http.Contracts;
using Toren.DotNet.Http.Errors;
using Toren.DotNet.Http.Models;

namespace Toren.DotNet.Http.Services;

public sealed class HttpRequestVariableResolver : IHttpRequestVariableResolver
{
    public Result<HttpRequestDefinition> Resolve(
        HttpRequestDefinition request,
        IReadOnlyDictionary<string, string> variables)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(variables);

        var uriResult = ReplaceVariables(request.Uri.OriginalString, variables);
        if (uriResult.IsFailure)
        {
            return Result.Failure<HttpRequestDefinition>(uriResult.Error);
        }

        if (!Uri.TryCreate(uriResult.Value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return Result.Failure<HttpRequestDefinition>(HttpRequestErrors.InvalidResolvedUri(uriResult.Value));
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in request.Headers)
        {
            var headerResult = ReplaceVariables(header.Value, variables);
            if (headerResult.IsFailure)
            {
                return Result.Failure<HttpRequestDefinition>(headerResult.Error);
            }

            headers[header.Key] = headerResult.Value;
        }

        string? body = null;
        if (request.Body is not null)
        {
            var bodyResult = ReplaceVariables(request.Body, variables);
            if (bodyResult.IsFailure)
            {
                return Result.Failure<HttpRequestDefinition>(bodyResult.Error);
            }

            body = bodyResult.Value;
        }

        return Result.Success(request with
        {
            Uri = uri,
            Headers = headers,
            Body = body,
        });
    }

    private static Result<string> ReplaceVariables(
        string value,
        IReadOnlyDictionary<string, string> variables)
    {
        var builder = new StringBuilder(value.Length);
        var position = 0;
        while (position < value.Length)
        {
            var start = value.IndexOf("{{", position, StringComparison.Ordinal);
            if (start < 0)
            {
                builder.Append(value, position, value.Length - position);
                break;
            }

            builder.Append(value, position, start - position);
            var end = value.IndexOf("}}", start + 2, StringComparison.Ordinal);
            if (end < 0)
            {
                return Result.Failure<string>(
                    HttpRequestErrors.InvalidVariablePlaceholder(value[start..]));
            }

            var name = value[(start + 2)..end].Trim();
            if (name.Length == 0)
            {
                return Result.Failure<string>(
                    HttpRequestErrors.InvalidVariablePlaceholder(value[start..(end + 2)]));
            }

            if (!variables.TryGetValue(name, out var replacement))
            {
                return Result.Failure<string>(HttpRequestErrors.MissingVariable(name));
            }

            builder.Append(replacement);
            position = end + 2;
        }

        return Result.Success(builder.ToString());
    }
}
