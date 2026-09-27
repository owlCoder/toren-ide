namespace Toren.Workspaces.Models;

public sealed record WorkspaceProject(
    string Path,
    string DisplayName,
    ProjectMetadata Metadata,
    IReadOnlyList<ProjectReferenceInfo> References);
