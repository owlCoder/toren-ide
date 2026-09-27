namespace Toren.Workspaces.Models;

public sealed record WorkspaceDescriptor(
    string Path,
    string DisplayName,
    WorkspaceKind Kind);
