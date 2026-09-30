namespace Toren.DotNet.Http.Models;

public sealed record HttpRequestHistoryEntry(
    DateTimeOffset CompletedAt,
    string? Name,
    string Method,
    string RequestTarget,
    int StatusCode,
    TimeSpan Duration);
