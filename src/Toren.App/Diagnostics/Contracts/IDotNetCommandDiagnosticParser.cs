using Toren.App.Diagnostics.Models;
using Toren.DotNet.Execution.Models;
using Toren.Workspaces.Models;

namespace Toren.App.Diagnostics.Contracts;

public interface IDotNetCommandDiagnosticParser
{
    IReadOnlyList<ProblemDiagnostic> Parse(
        WorkspaceDescriptor workspace,
        DotNetCommandResult result);
}
