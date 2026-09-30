using Toren.Core.Results;
using Toren.Http.Models;

namespace Toren.Http.Contracts;

public interface IHttpRequestExecutor
{
    Task<Result<HttpResponseSnapshot>> ExecuteAsync(
        HttpRequestDefinition request,
        CancellationToken cancellationToken = default);
}
