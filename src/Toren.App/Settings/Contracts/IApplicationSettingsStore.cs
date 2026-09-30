using Toren.App.Settings.Models;
using Toren.Core.Results;

namespace Toren.App.Settings.Contracts;

public interface IApplicationSettingsStore
{
    Task<Result<ApplicationSettings>> LoadAsync(CancellationToken cancellationToken = default);

    Task<Result<ApplicationSettings>> SaveAsync(
        ApplicationSettings settings,
        CancellationToken cancellationToken = default);
}
