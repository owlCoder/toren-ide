using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Toren.Core.Results;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Models;

namespace Toren.App.ViewModels;

public sealed partial class ExplorerViewModel(IWorkspaceTreeService workspaceTreeService) : ObservableObject
{
    private readonly IWorkspaceTreeService _workspaceTreeService = workspaceTreeService
        ?? throw new ArgumentNullException(nameof(workspaceTreeService));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isWorkspaceOpen;

    public ObservableCollection<WorkspaceNodeViewModel> Roots { get; } = new();

    public bool IsEmpty => !IsWorkspaceOpen;

    public Result<WorkspaceNode> Open(WorkspaceDescriptor workspace)
    {
        var result = _workspaceTreeService.CreateRoot(workspace);
        if (!result.IsSuccess)
        {
            return result;
        }

        Roots.Clear();
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
                node.SetChildren(result.Value);
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
}
