using Toren.App.Diagnostics.Contracts;
using Toren.Language.CSharp.Contracts;
using Toren.Language.CSharp.Models;

namespace Toren.App.Diagnostics.Services;

public sealed class DocumentDiagnosticsCoordinator : IDocumentDiagnosticsCoordinator
{
    private static readonly TimeSpan DefaultDebounceDelay = TimeSpan.FromMilliseconds(300);

    private readonly object _gate = new();
    private readonly ICSharpDiagnosticService _cSharpDiagnosticService;
    private readonly TimeSpan _debounceDelay;
    private CancellationTokenSource? _pendingAnalysis;
    private bool _disposed;

    public DocumentDiagnosticsCoordinator(
        ICSharpDiagnosticService cSharpDiagnosticService,
        TimeSpan? debounceDelay = null)
    {
        _cSharpDiagnosticService = cSharpDiagnosticService
            ?? throw new ArgumentNullException(nameof(cSharpDiagnosticService));
        _debounceDelay = debounceDelay ?? DefaultDebounceDelay;
    }

    public async Task<IReadOnlyList<CSharpDiagnostic>?> AnalyzeLatestAsync(
        CSharpSemanticContext context,
        bool debounce,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

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
            if (debounce && _debounceDelay > TimeSpan.Zero)
            {
                await Task.Delay(_debounceDelay, analysisCancellation.Token).ConfigureAwait(false);
            }

            var diagnostics = await _cSharpDiagnosticService
                .AnalyzeAsync(context, analysisCancellation.Token)
                .ConfigureAwait(false);

            lock (_gate)
            {
                return ReferenceEquals(_pendingAnalysis, analysisCancellation)
                    ? diagnostics
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
