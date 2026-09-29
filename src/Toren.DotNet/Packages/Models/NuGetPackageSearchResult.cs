namespace Toren.DotNet.Packages.Models;

public sealed record NuGetPackageSearchResult(
    string Id,
    string LatestVersion,
    long? TotalDownloads,
    string? Owners,
    string SourceName);
