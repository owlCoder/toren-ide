using Toren.App.Diagnostics.Models;
using Toren.Core.Results;

namespace Toren.App.Diagnostics.Contracts;

public interface IProblemsViewStateStore
{
    Task<Result<ProblemsViewState>> LoadAsync(CancellationToken cancellationToken = default);

    Task<Result<ProblemsViewState>> SaveAsync(
        ProblemsViewState state,
        CancellationToken cancellationToken = default);
}
