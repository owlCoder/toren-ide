using System.Text.Json;
using Toren.DotNet.Packages.Models;

namespace Toren.DotNet.Packages.Parsers;

public static class DotNetPackageJsonParser
{
    public static IReadOnlyList<NuGetPackageSearchResult> ParseSearch(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var document = JsonDocument.Parse(json);
        var results = new List<NuGetPackageSearchResult>();
        if (!document.RootElement.TryGetProperty("searchResult", out var searchResults)
            || searchResults.ValueKind != JsonValueKind.Array)
        {
            return results;
        }

        foreach (var sourceResult in searchResults.EnumerateArray())
        {
            var sourceName = GetString(sourceResult, "sourceName") ?? string.Empty;
            if (!sourceResult.TryGetProperty("packages", out var packages)
                || packages.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var package in packages.EnumerateArray())
            {
                var id = GetString(package, "id");
                var latestVersion = GetString(package, "latestVersion");
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(latestVersion))
                {
                    continue;
                }

                long? totalDownloads = null;
                if (package.TryGetProperty("totalDownloads", out var downloads)
                    && downloads.TryGetInt64(out var downloadCount))
                {
                    totalDownloads = downloadCount;
                }

                results.Add(new NuGetPackageSearchResult(
                    id,
                    latestVersion,
                    totalDownloads,
                    GetString(package, "owners"),
                    sourceName));
            }
        }

        return results;
    }

    public static IReadOnlyList<NuGetInstalledPackage> ParseInstalled(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var document = JsonDocument.Parse(json);
        var results = new List<NuGetInstalledPackage>();
        if (!document.RootElement.TryGetProperty("projects", out var projects)
            || projects.ValueKind != JsonValueKind.Array)
        {
            return results;
        }

        foreach (var project in projects.EnumerateArray())
        {
            var projectPath = GetString(project, "path") ?? string.Empty;
            if (!project.TryGetProperty("frameworks", out var frameworks)
                || frameworks.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var framework in frameworks.EnumerateArray())
            {
                var targetFramework = GetString(framework, "framework") ?? string.Empty;
                if (!framework.TryGetProperty("topLevelPackages", out var packages)
                    || packages.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var package in packages.EnumerateArray())
                {
                    var id = GetString(package, "id");
                    var requestedVersion = GetString(package, "requestedVersion");
                    var resolvedVersion = GetString(package, "resolvedVersion");
                    if (string.IsNullOrWhiteSpace(id)
                        || string.IsNullOrWhiteSpace(requestedVersion)
                        || string.IsNullOrWhiteSpace(resolvedVersion))
                    {
                        continue;
                    }

                    results.Add(new NuGetInstalledPackage(
                        projectPath,
                        targetFramework,
                        id,
                        requestedVersion,
                        resolvedVersion));
                }
            }
        }

        return results;
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
}
