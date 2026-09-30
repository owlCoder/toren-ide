using Toren.DotNet.Http.Contracts;
using Toren.DotNet.Http.Models;

namespace Toren.DotNet.Http.Services;

public sealed class HttpRequestHistory(int capacity = 50) : IHttpRequestHistory
{
    private readonly int _capacity = capacity > 0
        ? capacity
        : throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "History capacity must be positive.");
    private readonly List<HttpRequestHistoryEntry> _entries = [];

    public IReadOnlyList<HttpRequestHistoryEntry> Entries => _entries;

    public void Record(
        HttpRequestDefinition request,
        HttpResponseSnapshot response,
        DateTimeOffset completedAt)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(response);

        _entries.Insert(0, new HttpRequestHistoryEntry(
            completedAt,
            request.Name,
            request.Method,
            request.RequestTarget,
            response.StatusCode,
            response.Duration));

        if (_entries.Count > _capacity)
        {
            _entries.RemoveRange(_capacity, _entries.Count - _capacity);
        }
    }

    public void Clear() => _entries.Clear();
}
