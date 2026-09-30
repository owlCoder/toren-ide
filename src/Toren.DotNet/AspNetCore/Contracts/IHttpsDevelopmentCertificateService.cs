using Toren.Core.Results;
using Toren.DotNet.AspNetCore.Models;

namespace Toren.DotNet.AspNetCore.Contracts;

public interface IHttpsDevelopmentCertificateService
{
    Task<Result<HttpsDevelopmentCertificateState>> CheckAsync(
        CancellationToken cancellationToken = default);

    Task<Result<bool>> TrustAsync(CancellationToken cancellationToken = default);
}
