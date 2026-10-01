namespace Toren.Workspaces.Models;

/// <summary>Project properties, items and declared references read from one evaluation.</summary>
public sealed record ProjectEvaluation(
    ProjectMetadata Metadata,
    IReadOnlyList<ProjectReferenceInfo> References);
