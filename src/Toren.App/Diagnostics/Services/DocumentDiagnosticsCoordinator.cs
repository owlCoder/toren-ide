using Toren.App.Diagnostics.Contracts;
using Toren.Language.CSharp.Contracts;
using Toren.Language.CSharp.Models;

namespace Toren.App.Diagnostics.Services;

public sealed class DocumentDiagnosticsCoordinator : IDocumentDiagnosticsCoordinator
{
    private static readonly TimeSpan DefaultDebounceDelay = TimeSpan.FromMilliseconds(300);

    private readonly object _gate = new();
    private readonly ICSharpSyntaxService _cSharpSyntaxService;
    private readonly TimeSpan _debounceDelay;
    private CancellationTokenSource? _pendingAnalysis;
    private bool _disposed;

    public DocumentDiagnosticsCoordinator(
        ICSharpSyntaxService cSharpSyntaxService,
        TimeSpan? debounceDelay = null)
    {
        _cSharpSyntaxService = cSharpSyntaxService
            ?? throw new ArgumentNullException(nameof(cSharpSyntaxService));
        _debounceDelay = debounceDelay ?? DefaultDebounceDelay;
    }

    public async Task<IReadOnlyList<CSharpDiagnostic>?> AnalyzeLatestAsync(
        string path,
        string sourceText,
        bool debounce,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(sourceText);

        if (!Path.GetExtension(path).Equals(".cs", StringComparison.OrdinalIgnoreCase))
        {
            CancelPending();
            return Array.Empty<CSharpDiagnostic>();
        }

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

            var diagnostics = await _cSharpSyntaxService
                .AnalyzeAsync(sourceText, analysisCancellation.Token)
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
