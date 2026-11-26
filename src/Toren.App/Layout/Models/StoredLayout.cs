namespace Toren.App.Layout.Models;

public sealed record StoredLayout(
    int Version,
    string? WorkspacePath = null,
    double? SidebarWidth = null,
    double? ToolPanelHeight = null,
    string? SplitOrientation = null,
    double? SplitRatio = null,
    string? SecondaryDocumentPath = null,
    string[]? PinnedDocumentPaths = null,
    bool? SyncWithActiveDocument = null);
