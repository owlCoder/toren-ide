namespace Toren.Workspaces.Models;

public sealed record ProjectReferenceInfo(
    string Identity,
    ProjectReferenceKind Kind,
    string? ResolvedPath = null);
