namespace Toren.DotNet.Http.Models;

public sealed record HttpRequestDefinition(
    string Method,
    string RequestTarget,
    IReadOnlyDictionary<string, string> Headers,
    string? Body = null,
    string? Name = null)
{
    public HttpRequestDefinition(
        string method,
        Uri uri,
        IReadOnlyDictionary<string, string> headers,
        string? body = null,
        string? name = null)
        : this(
            method,
            (uri ?? throw new ArgumentNullException(nameof(uri))).OriginalString,
            headers,
            body,
            name)
    {
    }
}
