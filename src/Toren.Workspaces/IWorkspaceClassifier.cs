namespace Toren.Workspaces;

public interface IWorkspaceClassifier
{
    WorkspaceDescriptor ClassifyDirectory(string path);

    bool TryClassifyFile(string path, out WorkspaceDescriptor? descriptor);
}
