using Toren.Core.Results;
using Toren.DotNet.Http.Models;

namespace Toren.DotNet.Http.Contracts;

public interface IHttpEnvironmentProvider
{
    Task<Result<IReadOnlyList<HttpEnvironment>>> GetEnvironmentsAsync(
        string documentPath,
        CancellationToken cancellationToken = default);
}
