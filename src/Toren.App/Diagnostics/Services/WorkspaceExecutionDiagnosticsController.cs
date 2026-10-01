using Avalonia.Controls;
using Toren.App.Diagnostics.Contracts;
using Toren.App.Diagnostics.ViewModels;
using Toren.App.Execution.Models;
using Toren.App.Execution.ViewModels;
using Toren.App.ViewModels;
using Toren.DotNet.Execution.Models;

namespace Toren.App.Diagnostics.Services;

internal sealed class WorkspaceExecutionDiagnosticsController : IDisposable
{
    private const string SupplementalSourceKey = "dotnet.command";

    private readonly Window _window;
    private readonly WorkspaceExecutionViewModel _execution;
    private readonly IDotNetCommandDiagnosticParser _parser;
    private readonly ProblemsViewModel _problems;
    private readonly MainWindowViewModel _shell;
    private readonly CancellationTokenSource _lifetime = new();
    private bool _detached;

    private WorkspaceExecutionDiagnosticsController(
        Window window,
        WorkspaceExecutionViewModel execution,
        IDotNetCommandDiagnosticParser parser,
        MainWindowViewModel shell)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _execution = execution ?? throw new ArgumentNullException(nameof(execution));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _problems = shell.Problems;

        _execution.CommandCompleted += Execution_OnCommandCompleted;
        _window.Closed += Window_OnClosed;
    }

    public static void Attach(
        Window window,
        WorkspaceExecutionViewModel execution,
        IDotNetCommandDiagnosticParser parser,
        MainWindowViewModel shell)
    {
        _ = new WorkspaceExecutionDiagnosticsController(window, execution, parser, shell);
    }

    private async void Execution_OnCommandCompleted(
        object? sender,
        WorkspaceCommandCompletedEventArgs eventArgs)
    {
        if (_detached || !_shell.Explorer.IsWorkspaceOpen || !Path.GetFullPath(_shell.WorkspacePath).Equals(
                Path.GetFullPath(eventArgs.Workspace.Path),
                OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                    ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) return;

        var diagnostics = _parser.Parse(eventArgs.Workspace, eventArgs.Result);
        _problems.ReplaceSupplemental(SupplementalSourceKey, diagnostics);
        if (eventArgs.Result.Kind is not (DotNetCommandKind.Restore or DotNetCommandKind.Build
            or DotNetCommandKind.Rebuild or DotNetCommandKind.Clean or DotNetCommandKind.Publish)) return;

        try
        {
            // Restore/build/clean change assets and generated sources used by the language service.
            await _shell.RefreshWorkspaceDiagnosticsAsync(_lifetime.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
    }

    private void Window_OnClosed(object? sender, EventArgs eventArgs)
    {
        Dispose();
    }

    public void Dispose()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        _lifetime.Cancel();
        _lifetime.Dispose();
        _execution.CommandCompleted -= Execution_OnCommandCompleted;
        _window.Closed -= Window_OnClosed;
    }
}
