using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Toren.Language.CSharp.Services;

internal static class RoslynAnalyzerLoader
{
    public static ImmutableArray<DiagnosticAnalyzer> Load(IReadOnlyList<string> analyzerPaths)
    {
        ArgumentNullException.ThrowIfNull(analyzerPaths);

        if (analyzerPaths.Count == 0)
        {
            return [];
        }

        var loader = new AnalyzerAssemblyLoader();
        var analyzers = ImmutableArray.CreateBuilder<DiagnosticAnalyzer>();

        foreach (var path in analyzerPaths)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(path);
            loader.AddDependencyLocation(fullPath);

            try
            {
                var reference = new AnalyzerFileReference(fullPath, loader);
                analyzers.AddRange(reference.GetAnalyzers(LanguageNames.CSharp));
            }
            catch (IOException)
            {
                // A stale analyzer path should not take down language services.
            }
            catch (UnauthorizedAccessException)
            {
                // The project remains usable even when an analyzer cannot be read.
            }
            catch (BadImageFormatException)
            {
                // Ignore native or otherwise invalid analyzer assemblies.
            }
            catch (FileLoadException)
            {
                // Analyzer load failures are isolated from editor diagnostics.
            }
        }

        return analyzers
            .DistinctBy(analyzer => analyzer.GetType().AssemblyQualifiedName, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private sealed class AnalyzerAssemblyLoader : IAnalyzerAssemblyLoader
    {
        public void AddDependencyLocation(string fullPath)
        {
        }

        public Assembly LoadFromPath(string fullPath) => Assembly.LoadFrom(fullPath);
    }
}
