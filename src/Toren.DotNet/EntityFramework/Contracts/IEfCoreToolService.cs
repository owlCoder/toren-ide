using Toren.Core.Results;
using Toren.DotNet.EntityFramework.Models;

namespace Toren.DotNet.EntityFramework.Contracts;

public interface IEfCoreToolService
{
    Task<Result<EfCoreToolStatus>> DetectAsync(
        string projectPath,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<EfCoreMigrationInfo>>> ListMigrationsAsync(
        EfCoreProjectRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> AddMigrationAsync(
        EfCoreMigrationRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> RemoveMigrationAsync(
        EfCoreProjectRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> UpdateDatabaseAsync(
        EfCoreDatabaseUpdateRequest request,
        CancellationToken cancellationToken = default);
}
