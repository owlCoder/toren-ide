using Toren.App.Documents.Models;

namespace Toren.App.Layout.Models;

public sealed record WorkspaceLayoutState(
    double? SidebarWidth = null,
    double? ToolPanelHeight = null,
    EditorSplitOrientation SplitOrientation = EditorSplitOrientation.None,
    double? SplitRatio = null,
    string? SecondaryDocumentPath = null,
    string[]? PinnedDocumentPaths = null,
    bool SyncWithActiveDocument = true)
{
    public static WorkspaceLayoutState Default { get; } = new();
}



