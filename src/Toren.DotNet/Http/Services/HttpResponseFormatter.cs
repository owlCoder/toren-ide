using System.Text.Json;
using System.Text.Json.Nodes;
using Toren.DotNet.Http.Contracts;
using Toren.DotNet.Http.Models;

namespace Toren.DotNet.Http.Services;

public sealed class HttpResponseFormatter : IHttpResponseFormatter
{
    private static readonly JsonSerializerOptions IndentedJsonOptions = new()
    {
        WriteIndented = true,
    };

    public string FormatBody(HttpResponseSnapshot response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (string.IsNullOrWhiteSpace(response.Body) || !HasJsonContentType(response.Headers))
        {
            return response.Body;
        }

        try
        {
            var node = JsonNode.Parse(response.Body);
            return node?.ToJsonString(IndentedJsonOptions) ?? response.Body;
        }
        catch (JsonException)
        {
            return response.Body;
        }
    }

    private static bool HasJsonContentType(IReadOnlyDictionary<string, IReadOnlyList<string>> headers) =>
        headers.TryGetValue("Content-Type", out var values)
        && values.Any(static value => value.Contains("json", StringComparison.OrdinalIgnoreCase));
}
