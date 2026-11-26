using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Toren.App.ViewModels.Utilities;
using Toren.Core.Results;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Models;

namespace Toren.App.ViewModels;

/// <param name="workspaceTreeService">Provides the nodes of the workspace tree.</param>
/// <param name="yieldToUserInterface">
/// Lets pending input and rendering run before continuing. Supplied by the desktop shell;
/// without it nodes are added in one go.
/// </param>
public sealed partial class ExplorerViewModel(
    IWorkspaceTreeService workspaceTreeService,
    Func<Task>? yieldToUserInterface = null) : ObservableObject
{
    private readonly IWorkspaceTreeService _workspaceTreeService = workspaceTreeService
        ?? throw new ArgumentNullException(nameof(workspaceTreeService));
    private readonly Func<Task>? _yieldToUserInterface = yieldToUserInterface;

    private WorkspaceNodeViewModel? _selectedNode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isWorkspaceOpen;

    /// <summary>When on, the Explorer selects the file of the active editor tab.</summary>
    [ObservableProperty]
    private bool _syncWithActiveDocument = true;

    public ObservableCollection<WorkspaceNodeViewModel> Roots { get; } = new();

    public bool IsEmpty => !IsWorkspaceOpen;

    /// <summary>Raised after <see cref="RevealAsync"/> selected a node, so the view can scroll to it.</summary>
    public event EventHandler<WorkspaceNodeViewModel>? NodeRevealed;

    public Result<WorkspaceNode> Open(WorkspaceDescriptor workspace)
    {
        var result = _workspaceTreeService.CreateRoot(workspace);
        if (!result.IsSuccess)
        {
            return result;
        }

        Roots.Clear();
        _selectedNode = null;
        Roots.Add(new WorkspaceNodeViewModel(result.Value));
        IsWorkspaceOpen = true;
        return result;
    }

    public async Task<Result<IReadOnlyList<WorkspaceNode>>> ExpandAsync(
        WorkspaceNodeViewModel node,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (node.IsLoaded || node.IsLoading)
        {
            return Result.Success<IReadOnlyList<WorkspaceNode>>([]);
        }

        node.IsLoading = true;
        node.SetLoading();
        try
        {
            var result = await _workspaceTreeService
                .GetChildrenAsync(node.Node, cancellationToken)
                .ConfigureAwait(true);

            if (result.IsSuccess)
            {
                await node.SetChildrenAsync(result.Value, _yieldToUserInterface).ConfigureAwait(true);
            }
            else
            {
                node.SetError("Could not load contents. Collapse and expand to retry.");
            }

            return result;
        }
        finally
        {
            node.IsLoading = false;
        }
    }

    /// <summary>
    /// Expands the tree down to <paramref name="path"/> (loading folders as needed) and selects it.
    /// Returns false when the path is not part of the open workspace tree.
    /// </summary>
    public async Task<bool> RevealAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!IsWorkspaceOpen || Roots.Count == 0 || string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var target = Path.GetFullPath(path);
        var current = Roots[0];
        while (!PathComparisons.AreSame(current.Node.Path, target))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await EnsureLoadedAsync(current, cancellationToken).ConfigureAwait(true))
            {
                return false;
            }

            var next = FindChildContaining(current, target);
            if (next is null)
            {
                return false;
            }

            current.IsExpanded = true;
            current = next;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (_selectedNode is not null && !ReferenceEquals(_selectedNode, current))
        {
            _selectedNode.IsSelected = false;
        }

        current.IsSelected = true;
        _selectedNode = current;
        NodeRevealed?.Invoke(this, current);
        return true;
    }

    private async Task<bool> EnsureLoadedAsync(WorkspaceNodeViewModel node, CancellationToken cancellationToken)
    {
        if (!node.Node.CanExpand)
        {
            return false;
        }

        // The root (or a node the user just expanded) may still be loading.
        for (var attempt = 0; node.IsLoading && attempt < 100; attempt++)
        {
            await Task.Delay(50, cancellationToken).ConfigureAwait(true);
        }

        if (!node.IsLoaded)
        {
            await ExpandAsync(node, cancellationToken).ConfigureAwait(true);
        }

        return node.IsLoaded;
    }

    private static WorkspaceNodeViewModel? FindChildContaining(WorkspaceNodeViewModel parent, string target)
    {
        WorkspaceNodeViewModel? best = null;
        var bestLength = -1;
        foreach (var child in parent.Children)
        {
            if (child.IsPlaceholder
                || child.Node.Kind is WorkspaceNodeKind.References or WorkspaceNodeKind.Reference)
            {
                continue;
            }

            if (PathComparisons.AreSame(child.Node.Path, target))
            {
                return child;
            }

            // Folders contain their sub-paths; projects contain everything next to their project file.
            var directory = child.Node.Kind switch
            {
                WorkspaceNodeKind.Folder => child.Node.Path,
                WorkspaceNodeKind.Project => Path.GetDirectoryName(child.Node.Path),
                _ => null,
            };
            if (!string.IsNullOrEmpty(directory)
                && directory.Length > bestLength
                && PathComparisons.IsInside(directory, target))
            {
                best = child;
                bestLength = directory.Length;
            }
        }

        return best;
    }
}
