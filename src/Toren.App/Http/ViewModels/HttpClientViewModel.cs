using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Toren.DotNet.Http.Contracts;
using Toren.DotNet.Http.Models;

namespace Toren.App.Http.ViewModels;

public sealed partial class HttpClientViewModel(
    IHttpRequestDocumentParser parser,
    IHttpRequestVariableResolver variableResolver,
    IHttpRequestRunner requestRunner,
    IHttpResponseFormatter responseFormatter,
    IHttpRequestHistory requestHistory) : ObservableObject
{
    private readonly IHttpRequestDocumentParser _parser = parser
        ?? throw new ArgumentNullException(nameof(parser));
    private readonly IHttpRequestVariableResolver _variableResolver = variableResolver
        ?? throw new ArgumentNullException(nameof(variableResolver));
    private readonly IHttpRequestRunner _requestRunner = requestRunner
        ?? throw new ArgumentNullException(nameof(requestRunner));
    private readonly IHttpResponseFormatter _responseFormatter = responseFormatter
        ?? throw new ArgumentNullException(nameof(responseFormatter));
    private readonly IHttpRequestHistory _requestHistory = requestHistory
        ?? throw new ArgumentNullException(nameof(requestHistory));
    private readonly Dictionary<string, string> _variables = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _requestCancellation;
    private string? _documentPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedRequest))]
    [NotifyPropertyChangedFor(nameof(CanSend))]
    private int _selectedRequestIndex = -1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSend))]
    [NotifyPropertyChangedFor(nameof(CanCancel))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "Open a .http document to send requests.";

    [ObservableProperty]
    private string _responseStatus = "No response yet.";

    [ObservableProperty]
    private string _responseHeaders = string.Empty;

    [ObservableProperty]
    private string _responseBody = string.Empty;

    public ObservableCollection<HttpRequestDefinition> Requests { get; } = new();

    public ObservableCollection<HttpRequestHistoryEntry> History { get; } = new();

    public HttpRequestDefinition? SelectedRequest =>
        SelectedRequestIndex >= 0 && SelectedRequestIndex < Requests.Count
            ? Requests[SelectedRequestIndex]
            : null;

    public bool CanSend => !IsBusy && SelectedRequest is not null;

    public bool CanCancel => IsBusy;

    public void SetDocument(string? path, string? content)
    {
        _documentPath = string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
        Requests.Clear();
        SelectedRequestIndex = -1;

        if (_documentPath is null
            || !Path.GetExtension(_documentPath).Equals(".http", StringComparison.OrdinalIgnoreCase))
        {
            StatusText = "Open a .http document to send requests.";
            NotifyAvailability();
            return;
        }

        var parsed = _parser.Parse(content ?? string.Empty);
        if (parsed.IsFailure)
        {
            StatusText = parsed.Error.Message;
            NotifyAvailability();
            return;
        }

        foreach (var request in parsed.Value!)
        {
            Requests.Add(request);
        }

        SelectedRequestIndex = Requests.Count > 0 ? 0 : -1;
        StatusText = Requests.Count == 1
            ? "1 request available."
            : $"{Requests.Count} requests available.";
        NotifyAvailability();
    }

    public void SetVariables(IReadOnlyDictionary<string, string> variables)
    {
        ArgumentNullException.ThrowIfNull(variables);
        _variables.Clear();
        foreach (var variable in variables)
        {
            _variables[variable.Key] = variable.Value;
        }
    }

    public async Task SendSelectedAsync(CancellationToken cancellationToken = default)
    {
        if (!CanSend || SelectedRequest is not { } selectedRequest)
        {
            return;
        }

        var resolved = _variableResolver.Resolve(selectedRequest, _variables);
        if (resolved.IsFailure)
        {
            StatusText = resolved.Error.Message;
            return;
        }

        Cancel();
        _requestCancellation?.Dispose();
        _requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var activeCancellation = _requestCancellation;
        IsBusy = true;
        StatusText = $"Sending {resolved.Value!.Method} {resolved.Value.RequestTarget}...";

        try
        {
            var result = await _requestRunner
                .ExecuteAsync(resolved.Value, activeCancellation.Token)
                .ConfigureAwait(true);
            if (result.IsFailure)
            {
                StatusText = result.Error.Message;
                return;
            }

            var response = result.Value!;
            ResponseStatus = $"{response.StatusCode} {response.ReasonPhrase} · {response.Duration.TotalMilliseconds:F0} ms";
            ResponseHeaders = string.Join(
                System.Environment.NewLine,
                response.Headers
                    .OrderBy(static header => header.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(static header => $"{header.Key}: {string.Join(", ", header.Value)}"));
            ResponseBody = _responseFormatter.FormatBody(response);
            _requestHistory.Record(resolved.Value, response, DateTimeOffset.UtcNow);
            SynchronizeHistory();
            StatusText = $"Completed {resolved.Value.Method} {resolved.Value.RequestTarget}.";
        }
        catch (OperationCanceledException) when (activeCancellation.IsCancellationRequested)
        {
            StatusText = "HTTP request cancelled.";
        }
        finally
        {
            if (ReferenceEquals(_requestCancellation, activeCancellation))
            {
                _requestCancellation.Dispose();
                _requestCancellation = null;
            }

            IsBusy = false;
            NotifyAvailability();
        }
    }

    public void Cancel() => _requestCancellation?.Cancel();

    public void ClearHistory()
    {
        _requestHistory.Clear();
        History.Clear();
    }

    private void SynchronizeHistory()
    {
        History.Clear();
        foreach (var entry in _requestHistory.Entries)
        {
            History.Add(entry);
        }
    }

    private void NotifyAvailability()
    {
        OnPropertyChanged(nameof(SelectedRequest));
        OnPropertyChanged(nameof(CanSend));
        OnPropertyChanged(nameof(CanCancel));
    }
}
