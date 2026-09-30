namespace Toren.Http.Models;

public sealed record HttpRequestDefinition(
    string? Name,
    string Method,
    string Url,
    IReadOnlyDictionary<string, string> Headers,
    string? Body);

public sealed record HttpRequestFile(
    IReadOnlyDictionary<string, string> Variables,
    IReadOnlyList<HttpRequestDefinition> Requests);

public sealed record HttpResponseSnapshot(
    int StatusCode,
    string? ReasonPhrase,
    IReadOnlyDictionary<string, string> Headers,
    string Body,
    TimeSpan Duration);
