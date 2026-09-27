namespace Toren.Workspaces.Models;

public sealed record WorkspaceNode(string Path, string Name, WorkspaceNodeKind Kind)
{
    public bool CanExpand => Kind is WorkspaceNodeKind.Folder
        or WorkspaceNodeKind.Solution
        or WorkspaceNodeKind.Project
        or WorkspaceNodeKind.References;
}
