using Microsoft.CodeAnalysis;

namespace Toren.Language.CSharp.Services;

internal static class RoslynMetadataReferenceProvider
{
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
    private static readonly Lazy<MetadataReference[]> PlatformReferences = new(CreatePlatformReferences);

    public static IReadOnlyList<MetadataReference> GetReferences(
        IReadOnlyList<string>? projectReferencePaths = null)
    {
        if (projectReferencePaths is not null)
        {
            var resolvedPaths = projectReferencePaths
                .Where(static path => !string.IsNullOrWhiteSpace(path))
                .Select(Path.GetFullPath)
                .Where(File.Exists)
                .Distinct(PathComparer)
                .ToArray();
            if (resolvedPaths.Length > 0)
            {
                return CreateReferences(resolvedPaths);
            }
        }

        return PlatformReferences.Value;
    }

    private static MetadataReference[] CreatePlatformReferences()
    {
        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        var paths = string.IsNullOrWhiteSpace(trustedPlatformAssemblies)
            ? new[] { typeof(object).Assembly.Location, typeof(Enumerable).Assembly.Location }
            : trustedPlatformAssemblies.Split(
                Path.PathSeparator,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return CreateReferences(paths);
    }

    private static MetadataReference[] CreateReferences(IEnumerable<string> paths) =>
        paths
            .Where(File.Exists)
            .Select(Path.GetFullPath)
            .Distinct(PathComparer)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToArray();
}
