using Toren.Language.CSharp.Models;

namespace Toren.App.Diagnostics.Models;

public sealed record WorkspaceDiagnosticsSnapshot(
    IReadOnlyList<CSharpDocumentDiagnostics> DocumentDiagnostics,
    IReadOnlyList<ProblemDiagnostic> WorkspaceDiagnostics);
