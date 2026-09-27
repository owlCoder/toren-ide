namespace Toren.Workspaces;

public sealed record WorkspaceDescriptor(
    string Path,
    string DisplayName,
    WorkspaceKind Kind);
