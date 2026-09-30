namespace Toren.DotNet.Http.Models;

public sealed record HttpRequestDefinition(
    string Method,
    Uri Uri,
    IReadOnlyDictionary<string, string> Headers,
    string? Body = null,
    string? Name = null);
