namespace Toren.DotNet.Environment;

public interface IDotNetEnvironmentService
{
    Task<IReadOnlyList<DotNetSdkInfo>> GetInstalledSdksAsync(
        CancellationToken cancellationToken = default);
}
