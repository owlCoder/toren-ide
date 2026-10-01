using Toren.Workspaces.Contracts;
using Toren.Workspaces.Models;

namespace Toren.Workspaces.Services;

public static class WorkspaceClassifierExtensions
{
    /// <summary>
    /// Classifies a workspace path that may be either a folder or a solution/project file.
    /// Returns <see langword="null"/> for a file that is not a supported workspace.
    /// </summary>
    public static WorkspaceDescriptor? ClassifyPath(this IWorkspaceClassifier classifier, string path)
    {
        ArgumentNullException.ThrowIfNull(classifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (Directory.Exists(path))
        {
            return classifier.ClassifyDirectory(path);
        }

        return classifier.TryClassifyFile(path, out var workspace)
            ? workspace
            : null;
    }
}
