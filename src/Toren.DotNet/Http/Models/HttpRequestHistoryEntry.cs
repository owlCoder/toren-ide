namespace Toren.DotNet.Http.Models;

public sealed record HttpRequestHistoryEntry(
    DateTimeOffset CompletedAt,
    string? Name,
    string Method,
    Uri Uri,
    int StatusCode,
    TimeSpan Duration);
