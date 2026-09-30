namespace Toren.DotNet.EntityFramework.Models;

public sealed record EfCoreProjectRequest(
    string ProjectPath,
    string? StartupProjectPath = null,
    string? Context = null);

public sealed record EfCoreMigrationRequest(
    string ProjectPath,
    string MigrationName,
    string? StartupProjectPath = null,
    string? Context = null,
    string? OutputDirectory = null);

public sealed record EfCoreDatabaseUpdateRequest(
    string ProjectPath,
    string? Migration = null,
    string? StartupProjectPath = null,
    string? Context = null);

public sealed record EfCoreMigrationInfo(
    string Id,
    string Name,
    bool? Applied);

public sealed record EfCoreToolStatus(
    bool IsAvailable,
    string? Version,
    string? Details);
