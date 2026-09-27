using Toren.Core.Results;

namespace Toren.DotNet.Environment.Contracts;

public interface IDotNetSdkResolver
{
    Task<Result<string>> ResolveVersionAsync(
        string workingDirectory,
        CancellationToken cancellationToken = default);
}
