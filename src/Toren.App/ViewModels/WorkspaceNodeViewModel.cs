using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Toren.Workspaces.Models;

namespace Toren.App.ViewModels;

public sealed partial class WorkspaceNodeViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFolderClosed))]
    [NotifyPropertyChangedFor(nameof(IsFolderOpen))]
    private bool _isExpanded;

    public WorkspaceNodeViewModel(WorkspaceNode node, bool isExpanded = false, bool isPlaceholder = false)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
        IsExpanded = isExpanded;
        IsPlaceholder = isPlaceholder;
        IconKind = isPlaceholder ? ExplorerIconKind.None : ClassifyIcon(node);
        if (node.CanExpand)
        {
            Children.Add(CreateMessage("Loading…"));
        }
    }

    public WorkspaceNode Node { get; }

    public string Name => Node.Name;

    public bool IsPlaceholder { get; }

    public bool IsContent => !IsPlaceholder;

    public ExplorerIconKind IconKind { get; }

    public ObservableCollection<WorkspaceNodeViewModel> Children { get; } = new();

    public bool IsFolderClosed => IconKind == ExplorerIconKind.Folder && !IsExpanded;

    public bool IsFolderOpen => IconKind == ExplorerIconKind.Folder && IsExpanded;

    public bool IsSolution => IconKind == ExplorerIconKind.Solution;

    public bool IsProject => IconKind == ExplorerIconKind.Project;

    public bool IsTestProject => IconKind == ExplorerIconKind.TestProject;

    public bool IsReferences => IconKind == ExplorerIconKind.References;

    public bool IsReference => IconKind == ExplorerIconKind.Reference;

    public bool IsCodeFile => IconKind == ExplorerIconKind.Code;

    public bool IsDataFile => IconKind == ExplorerIconKind.Data;

    public bool IsMarkupFile => IconKind == ExplorerIconKind.Markup;

    public bool IsConfigFile => IconKind == ExplorerIconKind.Config;

    public bool IsDocumentFile => IconKind == ExplorerIconKind.Document;

    public bool IsContainerFile => IconKind == ExplorerIconKind.Container;

    public bool IsImageFile => IconKind == ExplorerIconKind.Image;

    public bool IsGenericFile => IconKind == ExplorerIconKind.File;

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

    private static ExplorerIconKind ClassifyIcon(WorkspaceNode node)
    {
        switch (node.Kind)
        {
            case WorkspaceNodeKind.Folder:
                return ExplorerIconKind.Folder;
            case WorkspaceNodeKind.Solution:
                return ExplorerIconKind.Solution;
            case WorkspaceNodeKind.Project:
                return node.Name.Contains(".Tests.", StringComparison.OrdinalIgnoreCase)
                    || node.Name.EndsWith(".Tests.csproj", StringComparison.OrdinalIgnoreCase)
                    ? ExplorerIconKind.TestProject
                    : ExplorerIconKind.Project;
            case WorkspaceNodeKind.References:
                return ExplorerIconKind.References;
            case WorkspaceNodeKind.Reference or WorkspaceNodeKind.SymbolicLink:
                return ExplorerIconKind.Reference;
            case WorkspaceNodeKind.File:
                return ClassifyFile(node.Name);
            default:
                throw new ArgumentOutOfRangeException(nameof(node));
        }
    }

    private static ExplorerIconKind ClassifyFile(string name)
    {
        if (name.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Dockerfile.", StringComparison.OrdinalIgnoreCase))
        {
            return ExplorerIconKind.Container;
        }

        if (name.Equals(".editorconfig", StringComparison.OrdinalIgnoreCase)
            || name.Equals("NuGet.Config", StringComparison.OrdinalIgnoreCase)
            || name.Equals("global.json", StringComparison.OrdinalIgnoreCase))
        {
            return ExplorerIconKind.Config;
        }

        return Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".cs" => ExplorerIconKind.Code,
            ".json" => ExplorerIconKind.Data,
            ".razor" or ".cshtml" or ".html" or ".xml" or ".axaml" or ".xaml"
                or ".props" or ".targets" => ExplorerIconKind.Markup,
            ".yml" or ".yaml" or ".config" or ".toml" => ExplorerIconKind.Config,
            ".md" or ".txt" => ExplorerIconKind.Document,
            ".png" or ".jpg" or ".jpeg" or ".svg" or ".webp" or ".ico" => ExplorerIconKind.Image,
            _ => ExplorerIconKind.File,
        };
    }
}
