using Toren.Core.Results;
using Toren.DotNet.Testing.Models;

namespace Toren.DotNet.Testing.Contracts;

public interface IDotNetTestDiscoveryService
{
    Task<Result<IReadOnlyList<DotNetTestCase>>> DiscoverAsync(
        DotNetTestDiscoveryRequest request,
        CancellationToken cancellationToken = default);
}
