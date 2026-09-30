using System.Net;
using NUnit.Framework;
using Toren.DotNet.Http.Models;
using Toren.DotNet.Http.Parsers;
using Toren.DotNet.Http.Services;

namespace Toren.UnitTests.Http;

[TestFixture]
public sealed class HttpRequestDocumentParserTests
{
    [Test]
    public void ParseMultipleRequestsPreservesMetadataHeadersAndBody()
    {
        const string source = """
            # @name listWidgets
            GET https://example.test/widgets
            Accept: application/json

            ###
            // @name createWidget
            POST https://example.test/widgets
            Content-Type: application/json
            X-Toren: test

            {"name":"demo"}
            """;

        var result = new HttpRequestDocumentParser().Parse(source);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Has.Count.EqualTo(2));
        var requests = result.Value!;
        Assert.Multiple(() =>
        {
            Assert.That(requests[0].Name, Is.EqualTo("listWidgets"));
            Assert.That(requests[0].Method, Is.EqualTo("GET"));
            Assert.That(requests[0].RequestTarget, Is.EqualTo("https://example.test/widgets"));
            Assert.That(requests[0].Headers["Accept"], Is.EqualTo("application/json"));
            Assert.That(requests[0].Body, Is.Null);
            Assert.That(requests[1].Name, Is.EqualTo("createWidget"));
            Assert.That(requests[1].Method, Is.EqualTo("POST"));
            Assert.That(requests[1].Headers["Content-Type"], Is.EqualTo("application/json"));
            Assert.That(requests[1].Headers["X-Toren"], Is.EqualTo("test"));
            Assert.That(requests[1].Body, Is.EqualTo("{\"name\":\"demo\"}"));
        });
    }

    [Test]
    public void ParsePreservesVariableBasedRequestTargetUntilResolution()
    {
        var parsed = new HttpRequestDocumentParser().Parse("GET {{baseUrl}}/api/widgets");

        Assert.That(parsed.IsSuccess, Is.True);
        Assert.That(parsed.Value, Has.Count.EqualTo(1));
        var resolved = new HttpRequestVariableResolver().Resolve(
            parsed.Value![0],
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["baseUrl"] = "https://example.test",
            });

        Assert.Multiple(() =>
        {
            Assert.That(resolved.IsSuccess, Is.True);
            Assert.That(resolved.Value!.RequestTarget, Is.EqualTo("https://example.test/api/widgets"));
        });
    }

    [Test]
    public void ParseRejectsRelativeRequestUriWithoutVariables()
    {
        var result = new HttpRequestDocumentParser().Parse("GET /api/widgets");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("http.request.uri.invalid"));
        });
    }

    [Test]
    public void ParseRejectsMalformedHeader()
    {
        var result = new HttpRequestDocumentParser().Parse("GET https://example.test/\nBrokenHeader");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("http.request.header.invalid"));
        });
    }

    [Test]
    public void ParseRejectsDocumentWithoutRequests()
    {
        var result = new HttpRequestDocumentParser().Parse("# comment only\n\n###\n// still empty");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("http.document.empty"));
        });
    }
}

[TestFixture]
public sealed class HttpRequestRunnerTests
{
    [Test]
    public async Task ExecuteMapsRequestAndResponseData()
    {
        var handler = new RecordingHandler(static (_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("{\"id\":42}"),
            };
            response.Headers.Add("X-Toren", "response");
            return Task.FromResult(response);
        });
        using var client = new HttpClient(handler, disposeHandler: true);
        var runner = new HttpRequestRunner(client);
        var request = new HttpRequestDefinition(
            "POST",
            "https://example.test/widgets",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Content-Type"] = "application/json",
                ["X-Request"] = "toren",
            },
            "{\"name\":\"demo\"}");

        var result = await runner.ExecuteAsync(request);

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(handler.Method, Is.EqualTo("POST"));
            Assert.That(handler.RequestUri, Is.EqualTo(new Uri("https://example.test/widgets")));
            Assert.That(handler.Body, Is.EqualTo("{\"name\":\"demo\"}"));
            Assert.That(handler.ContentType, Is.EqualTo("application/json"));
            Assert.That(handler.RequestHeaders["X-Request"], Is.EqualTo("toren"));
            Assert.That(result.Value!.StatusCode, Is.EqualTo(201));
            Assert.That(result.Value.Body, Is.EqualTo("{\"id\":42}"));
            Assert.That(result.Value.Headers["X-Toren"], Has.Count.EqualTo(1));
            Assert.That(result.Value.Headers["X-Toren"][0], Is.EqualTo("response"));
            Assert.That(result.Value.Duration, Is.GreaterThanOrEqualTo(TimeSpan.Zero));
        });
    }

    [Test]
    public async Task ExecuteRejectsUnresolvedVariableRequestTarget()
    {
        using var client = new HttpClient(new RecordingHandler(static (_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        var runner = new HttpRequestRunner(client);
        var request = new HttpRequestDefinition(
            "GET",
            "{{baseUrl}}/api/widgets",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        var result = await runner.ExecuteAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("http.variable.uri.invalid"));
        });
    }

    [Test]
    public async Task ExecuteReturnsOperationalErrorForNetworkFailure()
    {
        var handler = new RecordingHandler(static (_, _) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("network unavailable")));
        using var client = new HttpClient(handler, disposeHandler: true);
        var runner = new HttpRequestRunner(client);
        var request = CreateGetRequest();

        var result = await runner.ExecuteAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("http.request.execution.failed"));
            Assert.That(result.Error.Message, Does.Contain("network unavailable"));
        });
    }

    [Test]
    public void ExecutePropagatesCallerCancellation()
    {
        var handler = new RecordingHandler(static async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var client = new HttpClient(handler, disposeHandler: true);
        var runner = new HttpRequestRunner(client);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.CatchAsync<OperationCanceledException>(async () =>
            await runner.ExecuteAsync(CreateGetRequest(), cancellation.Token));
    }

    private static HttpRequestDefinition CreateGetRequest() =>
        new(
            "GET",
            "https://example.test/widgets",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _sendAsync = sendAsync;

        public string? Method { get; private set; }

        public Uri? RequestUri { get; private set; }

        public string? Body { get; private set; }

        public string? ContentType { get; private set; }

        public Dictionary<string, string> RequestHeaders { get; } = new(StringComparer.OrdinalIgnoreCase);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method.Method;
            RequestUri = request.RequestUri;
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            ContentType = request.Content?.Headers.ContentType?.MediaType;
            foreach (var header in request.Headers)
            {
                RequestHeaders[header.Key] = string.Join(",", header.Value);
            }

            return await _sendAsync(request, cancellationToken);
        }
    }
}
