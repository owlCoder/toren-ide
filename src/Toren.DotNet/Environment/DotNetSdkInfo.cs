namespace Toren.DotNet.Environment;

public sealed record DotNetSdkInfo(string Version, string BasePath)
{
    public bool IsPrerelease => Version.Contains('-', StringComparison.Ordinal);
}
