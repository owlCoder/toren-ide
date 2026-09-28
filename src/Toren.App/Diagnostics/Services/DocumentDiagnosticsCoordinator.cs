using Toren.App.Diagnostics.Contracts;
using Toren.Language.CSharp.Contracts;
using Toren.Language.CSharp.Models;

namespace Toren.App.Diagnostics.Services;

public sealed class DocumentDiagnosticsCoordinator : IDocumentDiagnosticsCoordinator
{
    private static readonly TimeSpan DefaultDebounceDelay = TimeSpan.FromMilliseconds(300);

    private readonly object _gate = new();
    private readonly ICSharpDiagnosticService _cSharpDiagnosticService;
    private readonly ICSharpSyntaxService _cSharpSyntaxService;
    private readonly Func<string, string, CancellationToken, Task<CSharpSemanticContext?>>? _semanticContextFactory;
    private readonly TimeSpan _debounceDelay;
    private CancellationTokenSource? _pendingAnalysis;
    private bool _disposed;

    public DocumentDiagnosticsCoordinator(
        ICSharpDiagnosticService cSharpDiagnosticService,
        ICSharpSyntaxService cSharpSyntaxService,
        Func<string, string, CancellationToken, Task<CSharpSemanticContext?>>? semanticContextFactory = null,
        TimeSpan? debounceDelay = null)
    {
        _cSharpDiagnosticService = cSharpDiagnosticService
            ?? throw new ArgumentNullException(nameof(cSharpDiagnosticService));
        _cSharpSyntaxService = cSharpSyntaxService
            ?? throw new ArgumentNullException(nameof(cSharpSyntaxService));
        _semanticContextFactory = semanticContextFactory;
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

            var semanticContext = _semanticContextFactory is null
                ? null
                : await _semanticContextFactory(path, sourceText, analysisCancellation.Token).ConfigureAwait(false);
            var diagnostics = semanticContext is null
                ? await _cSharpSyntaxService
                    .AnalyzeAsync(sourceText, analysisCancellation.Token)
                    .ConfigureAwait(false)
                : await _cSharpDiagnosticService
                    .AnalyzeAsync(semanticContext, analysisCancellation.Token)
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
