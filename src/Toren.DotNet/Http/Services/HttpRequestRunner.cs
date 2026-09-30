using System.Diagnostics;
using System.Text;
using Toren.Core.Results;
using Toren.DotNet.Http.Contracts;
using Toren.DotNet.Http.Errors;
using Toren.DotNet.Http.Models;

namespace Toren.DotNet.Http.Services;

public sealed class HttpRequestRunner(HttpClient httpClient) : IHttpRequestRunner
{
    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    public async Task<Result<HttpResponseSnapshot>> ExecuteAsync(
        HttpRequestDefinition request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var message = new HttpRequestMessage(new HttpMethod(request.Method), request.Uri);
        if (request.Body is not null)
        {
            message.Content = new StringContent(request.Body, Encoding.UTF8);
        }

        foreach (var header in request.Headers)
        {
            if (message.Headers.TryAddWithoutValidation(header.Key, header.Value))
            {
                continue;
            }

            message.Content ??= new ByteArrayContent([]);
            message.Content.Headers.Remove(header.Key);
            _ = message.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var response = await _httpClient
                .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            var body = response.Content is null
                ? string.Empty
                : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            var headers = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in response.Headers)
            {
                headers[header.Key] = header.Value.ToArray();
            }

            if (response.Content is not null)
            {
                foreach (var header in response.Content.Headers)
                {
                    headers[header.Key] = header.Value.ToArray();
                }
            }

            return Result.Success(new HttpResponseSnapshot(
                (int)response.StatusCode,
                response.ReasonPhrase ?? string.Empty,
                headers,
                body,
                stopwatch.Elapsed));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result.Failure<HttpResponseSnapshot>(
                HttpRequestErrors.ExecutionFailed("The request timed out."));
        }
        catch (HttpRequestException exception)
        {
            return Result.Failure<HttpResponseSnapshot>(
                HttpRequestErrors.ExecutionFailed(exception.Message));
        }
    }
}
