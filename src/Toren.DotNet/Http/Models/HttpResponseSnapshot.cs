namespace Toren.DotNet.Http.Models;

public sealed record HttpResponseSnapshot(
    int StatusCode,
    string ReasonPhrase,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Headers,
    string Body,
    TimeSpan Duration);
