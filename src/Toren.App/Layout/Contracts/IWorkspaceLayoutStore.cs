using System;
using System.Collections.Generic;
using System.Text;
using Toren.App.Layout.Models;
using Toren.Core.Results;

namespace Toren.App.Layout.Contracts;

public interface IWorkspaceLayoutStore
{
    Task<Result<WorkspaceLayoutState>> LoadAsync(
    string workspacePath,
    CancellationToken cancellationToken = default);

    Task<Result<WorkspaceLayoutState>> SaveAsync(
        string workspacePath,
        WorkspaceLayoutState layout,
        CancellationToken cancellationToken = default);
}


