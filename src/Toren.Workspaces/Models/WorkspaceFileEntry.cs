namespace Toren.Workspaces.Models;

public sealed record WorkspaceFileEntry(
    string Path,
    string RelativePath,
    string Name);
