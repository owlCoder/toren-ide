using Toren.Containers.Models;
using Toren.Core.Results;

namespace Toren.Containers.Contracts;

public interface IDockerComposeService
{
    Task<Result<DockerComposeToolStatus>> DetectAsync(CancellationToken cancellationToken = default);

    Task<Result<bool>> UpAsync(
        DockerComposeRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> DownAsync(
        DockerComposeRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> BuildAsync(
        DockerComposeRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<string>> GetLogsAsync(
        DockerComposeLogsRequest request,
        CancellationToken cancellationToken = default);
}
