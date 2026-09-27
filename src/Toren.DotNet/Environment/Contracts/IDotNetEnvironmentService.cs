using Toren.Core.Results;
using Toren.DotNet.Environment.Models;

namespace Toren.DotNet.Environment.Contracts;

public interface IDotNetEnvironmentService
{
    Task<Result<IReadOnlyList<DotNetSdkInfo>>> GetInstalledSdksAsync(
        CancellationToken cancellationToken = default);
}
