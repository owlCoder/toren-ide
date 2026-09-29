using Avalonia.Controls;
using Toren.App.Diagnostics.Contracts;
using Toren.App.Diagnostics.ViewModels;
using Toren.App.Execution.Models;
using Toren.App.Execution.ViewModels;

namespace Toren.App.Diagnostics.Services;

internal sealed class WorkspaceExecutionDiagnosticsController
{
    private const string SupplementalSourceKey = "dotnet.command";

    private readonly Window _window;
    private readonly WorkspaceExecutionViewModel _execution;
    private readonly IDotNetCommandDiagnosticParser _parser;
    private readonly ProblemsViewModel _problems;
    private bool _detached;

    private WorkspaceExecutionDiagnosticsController(
        Window window,
        WorkspaceExecutionViewModel execution,
        IDotNetCommandDiagnosticParser parser,
        ProblemsViewModel problems)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _execution = execution ?? throw new ArgumentNullException(nameof(execution));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _problems = problems ?? throw new ArgumentNullException(nameof(problems));

        _execution.CommandCompleted += Execution_OnCommandCompleted;
        _window.Closed += Window_OnClosed;
    }

    public static void Attach(
        Window window,
        WorkspaceExecutionViewModel execution,
        IDotNetCommandDiagnosticParser parser,
        ProblemsViewModel problems)
    {
        _ = new WorkspaceExecutionDiagnosticsController(window, execution, parser, problems);
    }

    private void Execution_OnCommandCompleted(
        object? sender,
        WorkspaceCommandCompletedEventArgs eventArgs)
    {
        var diagnostics = _parser.Parse(eventArgs.Workspace, eventArgs.Result);
        _problems.ReplaceSupplemental(SupplementalSourceKey, diagnostics);
    }

    private void Window_OnClosed(object? sender, EventArgs eventArgs)
    {
        Detach();
    }

    private void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        _execution.CommandCompleted -= Execution_OnCommandCompleted;
        _window.Closed -= Window_OnClosed;
    }
}
