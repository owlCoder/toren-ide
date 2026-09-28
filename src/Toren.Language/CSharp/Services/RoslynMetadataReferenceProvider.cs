using Microsoft.CodeAnalysis;

namespace Toren.Language.CSharp.Services;

internal static class RoslynMetadataReferenceProvider
{
    private static readonly Lazy<MetadataReference[]> References = new(CreateReferences);

    public static IReadOnlyList<MetadataReference> GetReferences() => References.Value;

    private static MetadataReference[] CreateReferences()
    {
        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        var paths = string.IsNullOrWhiteSpace(trustedPlatformAssemblies)
            ? new[] { typeof(object).Assembly.Location, typeof(Enumerable).Assembly.Location }
            : trustedPlatformAssemblies.Split(
                Path.PathSeparator,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return paths
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToArray();
    }
}
