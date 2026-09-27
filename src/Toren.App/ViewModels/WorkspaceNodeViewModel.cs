using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Toren.Workspaces.Models;

namespace Toren.App.ViewModels;

public sealed partial class WorkspaceNodeViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isExpanded;

    public WorkspaceNodeViewModel(WorkspaceNode node, bool isExpanded = false, bool isPlaceholder = false)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
        IsExpanded = isExpanded;
        IsPlaceholder = isPlaceholder;
        if (node.CanExpand)
        {
            Children.Add(CreateMessage("Loading…"));
        }
    }

    public WorkspaceNode Node { get; }

    public string Name => Node.Name;

    public bool IsPlaceholder { get; }

    public bool IsContent => !IsPlaceholder;

    public ObservableCollection<WorkspaceNodeViewModel> Children { get; } = new();

    public bool IsFolder => Node.Kind == WorkspaceNodeKind.Folder;

    public bool IsSolution => Node.Kind == WorkspaceNodeKind.Solution;

    public bool IsProject => Node.Kind == WorkspaceNodeKind.Project;

    public bool IsReferences => Node.Kind == WorkspaceNodeKind.References;

    public bool IsFile => !IsPlaceholder && Node.Kind is (
        WorkspaceNodeKind.File or WorkspaceNodeKind.Reference or WorkspaceNodeKind.SymbolicLink);

    internal bool IsLoaded { get; private set; }

    internal bool IsLoading { get; set; }

    internal void SetLoading()
    {
        Children.Clear();
        Children.Add(CreateMessage("Loading…"));
    }

    internal void SetChildren(IReadOnlyList<WorkspaceNode> children)
    {
        Children.Clear();
        foreach (var child in children)
        {
            Children.Add(new WorkspaceNodeViewModel(child));
        }

        IsLoaded = true;
    }

    internal void SetError(string message)
    {
        Children.Clear();
        Children.Add(CreateMessage(message));
    }

    private static WorkspaceNodeViewModel CreateMessage(string message) =>
        new(new WorkspaceNode(string.Empty, message, WorkspaceNodeKind.File), isPlaceholder: true);
}
