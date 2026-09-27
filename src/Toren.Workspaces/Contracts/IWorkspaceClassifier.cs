using Toren.Workspaces.Models;

namespace Toren.Workspaces.Contracts;

public interface IWorkspaceClassifier
{
    WorkspaceDescriptor ClassifyDirectory(string path);

    bool TryClassifyFile(string path, out WorkspaceDescriptor? descriptor);
}
