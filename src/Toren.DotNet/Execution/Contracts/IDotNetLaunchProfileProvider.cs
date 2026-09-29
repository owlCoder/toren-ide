using Toren.Core.Results;
using Toren.DotNet.Execution.Models;

namespace Toren.DotNet.Execution.Contracts;

public interface IDotNetLaunchProfileProvider
{
    Task<Result<IReadOnlyList<DotNetLaunchProfile>>> GetProfilesAsync(
        string projectPath,
        CancellationToken cancellationToken = default);
}
