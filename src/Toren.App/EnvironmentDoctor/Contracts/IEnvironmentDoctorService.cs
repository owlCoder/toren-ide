using Toren.App.EnvironmentDoctor.Models;
using Toren.Core.Results;

namespace Toren.App.EnvironmentDoctor.Contracts;

public interface IEnvironmentDoctorService
{
    Task<Result<EnvironmentDoctorReport>> CheckAsync(
        string workspacePath,
        CancellationToken cancellationToken = default);
}
