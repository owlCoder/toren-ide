using Toren.DotNet.Http.Models;

namespace Toren.DotNet.Http.Contracts;

public interface IHttpRequestHistory
{
    IReadOnlyList<HttpRequestHistoryEntry> Entries { get; }

    void Record(
        HttpRequestDefinition request,
        HttpResponseSnapshot response,
        DateTimeOffset completedAt);

    void Clear();
}
