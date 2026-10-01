using Toren.Core.Execution.Models;

namespace Toren.Workspaces.Adapters;

internal static class MsBuildEvaluationRequest
{
    /// <summary>
    /// Creates a <c>dotnet</c> request that runs next to the given project or solution. The
    /// .NET host selects the SDK from the <c>global.json</c> above its working directory, so
    /// evaluation must start there to use the same SDK as building the workspace does.
    /// </summary>
    public static ProcessRequest Create(string projectOrSolutionPath, params string[] arguments)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(projectOrSolutionPath));
        return new ProcessRequest(
            "dotnet",
            arguments,
            // A missing directory would fail the process start and hide MSBuild's own error.
            Directory.Exists(directory) ? directory : null);
    }
}
