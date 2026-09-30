using System.Diagnostics;
using System.Text;
using Toren.Core.Results;
using Toren.Http.Contracts;
using Toren.Http.Models;

namespace Toren.Http.Services;

public sealed class HttpRequestExecutor(HttpClient httpClient) : IHttpRequestExecutor
{
    private const string InvalidUrlCode = "http.request.url-invalid";
    private const string RequestFailedCode = "http.request.failed";
    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    public async Task<Result<HttpResponseSnapshot>> ExecuteAsync(
        HttpRequestDefinition request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return Result.Failure<HttpResponseSnapshot>(new OperationError(
                InvalidUrlCode,
                $"HTTP request URL '{request.Url}' is not a valid HTTP or HTTPS URL."));
        }

        using var message = new HttpRequestMessage(new HttpMethod(request.Method), uri);
        if (request.Body is not null)
        {
            message.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(request.Body));
        }

        foreach (var pair in request.Headers)
        {
            if (message.Headers.TryAddWithoutValidation(pair.Key, pair.Value))
            {
                continue;
            }

            message.Content ??= new ByteArrayContent([]);
            _ = message.Content.Headers.TryAddWithoutValidation(pair.Key, pair.Value);
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var response = await _httpClient
                .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in response.Headers)
            {
                headers[pair.Key] = string.Join(", ", pair.Value);
            }

            foreach (var pair in response.Content.Headers)
            {
                headers[pair.Key] = string.Join(", ", pair.Value);
            }

            return Result.Success(new HttpResponseSnapshot(
                (int)response.StatusCode,
                response.ReasonPhrase,
                headers,
                body,
                stopwatch.Elapsed));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            stopwatch.Stop();
            return Result.Failure<HttpResponseSnapshot>(new OperationError(
                RequestFailedCode,
                exception.Message));
        }
    }
}
