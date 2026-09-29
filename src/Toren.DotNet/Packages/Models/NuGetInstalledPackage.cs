namespace Toren.DotNet.Packages.Models;

public sealed record NuGetInstalledPackage(
    string ProjectPath,
    string TargetFramework,
    string Id,
    string RequestedVersion,
    string ResolvedVersion);
