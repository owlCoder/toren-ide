using Toren.DotNet.Execution.Models;
using Toren.Workspaces.Models;

namespace Toren.App.Execution.Models;

public sealed class WorkspaceCommandCompletedEventArgs(
    WorkspaceDescriptor workspace,
    DotNetCommandResult result) : EventArgs
{
    public WorkspaceDescriptor Workspace { get; } = workspace
        ?? throw new ArgumentNullException(nameof(workspace));

    public DotNetCommandResult Result { get; } = result
        ?? throw new ArgumentNullException(nameof(result));
}
