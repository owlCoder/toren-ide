using Toren.Core.Results;
using Toren.DotNet.Packages.Models;

namespace Toren.DotNet.Packages.Contracts;

public interface IDotNetPackageService
{
    Task<Result<IReadOnlyList<NuGetPackageSearchResult>>> SearchAsync(
        string workingDirectory,
        string query,
        IReadOnlyList<string>? sources = null,
        int skip = 0,
        int take = 20,
        bool includePrerelease = false,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<NuGetInstalledPackage>>> GetInstalledAsync(
        string projectPath,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> AddAsync(
        string projectPath,
        string packageId,
        string? version = null,
        string? source = null,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> UpdateAsync(
        string projectPath,
        string packageId,
        string? version = null,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> RemoveAsync(
        string projectPath,
        string packageId,
        CancellationToken cancellationToken = default);
}
