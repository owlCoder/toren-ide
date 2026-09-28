using Toren.App.Diagnostics.Contracts;
using Toren.App.Diagnostics.Models;
using Toren.App.Editor.Contracts;
using Toren.Language.CSharp.Contracts;
using Toren.Language.CSharp.Models;

namespace Toren.App.Diagnostics.Services;

public sealed class WorkspaceDiagnosticsCoordinator(
    ICSharpSemanticContextProvider semanticContextProvider,
    ICSharpWorkspaceDiagnosticService workspaceDiagnosticService,
    ICSharpSyntaxService syntaxService) : IWorkspaceDiagnosticsCoordinator
{
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private readonly object _gate = new();
    private readonly ICSharpSemanticContextProvider _semanticContextProvider = semanticContextProvider
        ?? throw new ArgumentNullException(nameof(semanticContextProvider));
    private readonly ICSharpWorkspaceDiagnosticService _workspaceDiagnosticService = workspaceDiagnosticService
        ?? throw new ArgumentNullException(nameof(workspaceDiagnosticService));
    private readonly ICSharpSyntaxService _syntaxService = syntaxService
        ?? throw new ArgumentNullException(nameof(syntaxService));
    private CancellationTokenSource? _pendingAnalysis;
    private bool _disposed;

    public async Task<WorkspaceDiagnosticsSnapshot?> AnalyzeLatestAsync(
        string workspacePath,
        IReadOnlyList<CSharpSourceDocument> openDocuments,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        ArgumentNullException.ThrowIfNull(openDocuments);

        CancellationTokenSource analysisCancellation;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _pendingAnalysis?.Cancel();
            analysisCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _pendingAnalysis = analysisCancellation;
        }

        try
        {
            var contexts = await _semanticContextProvider
                .CreateWorkspaceProjectContextsAsync(
                    workspacePath,
                    openDocuments,
                    analysisCancellation.Token)
                .ConfigureAwait(false);
            if (contexts is null)
            {
                return null;
            }

            var diagnostics = new List<CSharpDocumentDiagnostics>();
            foreach (var projectContext in contexts.ProjectContexts)
            {
                analysisCancellation.Token.ThrowIfCancellationRequested();
                var projectDiagnostics = await _workspaceDiagnosticService
                    .AnalyzeDocumentsAsync(
                        projectContext.SemanticContext,
                        projectContext.DocumentPaths,
                        analysisCancellation.Token)
                    .ConfigureAwait(false);
                diagnostics.AddRange(projectDiagnostics);
            }

            foreach (var document in contexts.LooseDocuments)
            {
                analysisCancellation.Token.ThrowIfCancellationRequested();
                var syntaxDiagnostics = await _syntaxService
                    .AnalyzeAsync(document.Text, analysisCancellation.Token)
                    .ConfigureAwait(false);
                diagnostics.Add(new CSharpDocumentDiagnostics(
                    Path.GetFullPath(document.Path),
                    syntaxDiagnostics));
            }

            var orderedDiagnostics = diagnostics
                .GroupBy(item => Path.GetFullPath(item.FilePath), PathComparer)
                .Select(group => group.Last())
                .OrderBy(item => item.FilePath, PathComparer)
                .ToArray();
            IReadOnlyList<ProblemDiagnostic> workspaceDiagnostics = contexts.ProjectSystemError.IsNone
                ? []
                : [new ProblemDiagnostic(
                    contexts.ProjectSystemError.Code,
                    contexts.ProjectSystemError.Message,
                    ProblemSeverity.Error,
                    "Project system")];
            var snapshot = new WorkspaceDiagnosticsSnapshot(orderedDiagnostics, workspaceDiagnostics);

            lock (_gate)
            {
                return ReferenceEquals(_pendingAnalysis, analysisCancellation)
                    ? snapshot
                    : null;
            }
        }
        catch (OperationCanceledException) when (analysisCancellation.IsCancellationRequested)
        {
            return null;
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_pendingAnalysis, analysisCancellation))
                {
                    _pendingAnalysis = null;
                }
            }

            analysisCancellation.Dispose();
        }
    }

    public void CancelPending()
    {
        lock (_gate)
        {
            _pendingAnalysis?.Cancel();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _pendingAnalysis?.Cancel();
            _pendingAnalysis = null;
        }
    }
}
