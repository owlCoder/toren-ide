using Toren.Core.Results;
using Toren.DotNet.Http.Models;

namespace Toren.DotNet.Http.Contracts;

public interface IHttpRequestRunner
{
    Task<Result<HttpResponseSnapshot>> ExecuteAsync(
        HttpRequestDefinition request,
        CancellationToken cancellationToken = default);
}
