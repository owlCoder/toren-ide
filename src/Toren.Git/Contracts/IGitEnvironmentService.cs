using Toren.Core.Results;

namespace Toren.Git.Contracts;

public interface IGitEnvironmentService
{
    Task<Result<string>> GetVersionAsync(CancellationToken cancellationToken = default);
}
