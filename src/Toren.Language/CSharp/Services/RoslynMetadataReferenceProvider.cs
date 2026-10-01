using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;

namespace Toren.Language.CSharp.Services;

internal static class RoslynMetadataReferenceProvider
{
    // Bounds the bookkeeping entries; the references themselves are held weakly.
    private const int MaximumCachedReferences = 4096;

    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
    private static readonly Lazy<MetadataReference[]> PlatformReferences = new(CreatePlatformReferences);

    // Projects of a workspace mostly compile against the same assemblies, and every editor
    // request compiles again. Roslyn reads the whole assembly image when a reference is created,
    // so a reference is shared while any compilation still uses it. Holding it weakly keeps the
    // images from staying in memory once analysis stops.
    private static readonly ConcurrentDictionary<string, CachedReference> Cache = new(PathComparer);

    public static IReadOnlyList<MetadataReference> GetReferences(
        IReadOnlyList<string>? projectReferencePaths = null)
    {
        if (projectReferencePaths is not null)
        {
            var references = CreateReferences(projectReferencePaths);
            if (references.Length > 0)
            {
                return references;
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

    private static MetadataReference[] CreateReferences(IEnumerable<string> paths)
    {
        var references = new List<MetadataReference>();
        var seen = new HashSet<string>(PathComparer);
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(path);
            if (!seen.Add(fullPath))
            {
                continue;
            }

            var file = new FileInfo(fullPath);
            if (file.Exists)
            {
                references.Add(GetReference(fullPath, file.LastWriteTimeUtc, file.Length));
            }
        }

        return references.ToArray();
    }

    private static MetadataReference GetReference(string fullPath, DateTime lastWriteTimeUtc, long length)
    {
        // A rebuilt assembly has a new timestamp or size and gets a fresh reference.
        if (Cache.TryGetValue(fullPath, out var cached)
            && cached.LastWriteTimeUtc == lastWriteTimeUtc
            && cached.Length == length
            && cached.Reference.TryGetTarget(out var shared))
        {
            return shared;
        }

        if (Cache.Count >= MaximumCachedReferences)
        {
            Cache.Clear();
        }

        var reference = MetadataReference.CreateFromFile(fullPath);
        Cache[fullPath] = new CachedReference(lastWriteTimeUtc, length, new WeakReference<MetadataReference>(reference));
        return reference;
    }

    private sealed record CachedReference(
        DateTime LastWriteTimeUtc,
        long Length,
        WeakReference<MetadataReference> Reference);
}
