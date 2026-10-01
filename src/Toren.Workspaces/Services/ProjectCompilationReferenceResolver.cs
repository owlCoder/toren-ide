using Toren.Core.Results;
using Toren.Workspaces.Errors;
using Toren.Workspaces.Models;

namespace Toren.Workspaces.Services;

public static class ProjectCompilationReferenceResolver
{
    /// <summary>
    /// Returns the compiler reference assemblies of an evaluated project. Fails when the
    /// design-time targets could not resolve them, or when a resolved assembly is not on disk
    /// yet, so callers do not report false semantic errors for an unbuilt or unrestored project.
    /// </summary>
    public static Result<IReadOnlyList<string>> Resolve(ProjectMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        if (!metadata.CompilerInputsError.IsNone)
        {
            return Result.Failure<IReadOnlyList<string>>(metadata.CompilerInputsError);
        }

        if (metadata.ReferencePaths is not { } referencePaths)
        {
            return Result.Failure<IReadOnlyList<string>>(
                ProjectCompilationReferenceErrors.ResolutionFailed(
                    "Compiler references have not been resolved for this project."));
        }

        // Existence is checked on every call because building a referenced project creates
        // the assembly without changing the evaluated path.
        foreach (var path in referencePaths)
        {
            if (!File.Exists(path))
            {
                return Result.Failure<IReadOnlyList<string>>(
                    ProjectCompilationReferenceErrors.ResolutionFailed(
                        $"Reference '{path}' is unavailable. Build the solution to generate project references."));
            }
        }

        return Result.Success(referencePaths);
    }
}
