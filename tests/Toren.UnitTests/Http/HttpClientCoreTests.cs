using System.Net;
using System.Text;
using NUnit.Framework;
using Toren.Http.Models;
using Toren.Http.Services;

namespace Toren.UnitTests.Http;

[TestFixture]
public sealed class HttpClientCoreTests
{
    [Test]
    public void ParserReadsVariablesNamedRequestsHeadersAndBody()
    {
        const string text = """
            @host = https://localhost:7123
            @token = abc

            ### Create item
            POST {{host}}/api/items
            Authorization: Bearer {{token}}
            Content-Type: application/json

            {"name":"demo"}

            ### List items
            GET {{host}}/api/items
            """;
        var parser = new HttpRequestFileParser();

        var result = parser.Parse(text);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value!.Variables["host"], Is.EqualTo("https://localhost:7123"));
            Assert.That(result.Value.Requests, Has.Count.EqualTo(2));
            Assert.That(result.Value.Requests[0].Name, Is.EqualTo("Create item"));
            Assert.That(result.Value.Requests[0].Method, Is.EqualTo("POST"));
            Assert.That(result.Value.Requests[0].Headers["Content-Type"], Is.EqualTo("application/json"));
            Assert.That(result.Value.Requests[0].Body, Is.EqualTo("{\"name\":\"demo\"}"));
            Assert.That(result.Value.Requests[1].Method, Is.EqualTo("GET"));
        });
    }

    [Test]
    public void EnvironmentResolverOverridesFileVariablesAcrossRequestParts()
    {
        var request = new HttpRequestDefinition(
            "Get item",
            "GET",
            "{{baseUrl}}/items/{{id}}",
            new Dictionary<string, string> { ["X-Token"] = "{{token}}" },
            "{\"id\":\"{{id}}\"}");
        var resolver = new HttpEnvironmentResolver();

        var result = resolver.Resolve(
            request,
            new Dictionary<string, string>
            {
                ["baseUrl"] = "https://localhost:7001",
                ["id"] = "1",
                ["token"] = "file-token",
            },
            new Dictionary<string, string>
            {
                ["id"] = "42",
                ["token"] = "environment-token",
            });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value!.Url, Is.EqualTo("https://localhost:7001/items/42"));
            Assert.That(result.Value.Headers["X-Token"], Is.EqualTo("environment-token"));
            Assert.That(result.Value.Body, Is.EqualTo("{\"id\":\"42\"}"));
        });
    }

    [Test]
    public async Task ExecutorReturnsStructuredResponseAndPreservesRequestContent()
    {
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        var executor = new HttpRequestExecutor(client);
        var request = new HttpRequestDefinition(
            null,
            "POST",
            "https://example.test/api/items",
            new Dictionary<string, string>
            {
                ["X-Test"] = "value",
                ["Content-Type"] = "application/json",
            },
            "{\"name\":\"demo\"}");

        var result = await executor.ExecuteAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value!.StatusCode, Is.EqualTo(201));
            Assert.That(result.Value.Headers["X-Response"], Is.EqualTo("yes"));
            Assert.That(result.Value.Body, Is.EqualTo("created"));
            Assert.That(handler.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(handler.RequestUri, Is.EqualTo(new Uri("https://example.test/api/items")));
            Assert.That(handler.RequestHeader, Is.EqualTo("value"));
            Assert.That(handler.ContentType, Is.EqualTo("application/json"));
            Assert.That(handler.Body, Is.EqualTo("{\"name\":\"demo\"}"));
        });
    }

    [Test]
    public void EnvironmentResolverReportsUndefinedVariable()
    {
        var resolver = new HttpEnvironmentResolver();
        var request = new HttpRequestDefinition(null, "GET", "{{missing}}/items", new Dictionary<string, string>(), null);

        var result = resolver.Resolve(request, new Dictionary<string, string>());

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("http.environment.variable-missing"));
        });
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }

        public Uri? RequestUri { get; private set; }

        public string? RequestHeader { get; private set; }

        public string? ContentType { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            RequestHeader = request.Headers.GetValues("X-Test").Single();
            ContentType = request.Content?.Headers.ContentType?.MediaType;
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            var response = new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("created", Encoding.UTF8, "text/plain"),
            };
            response.Headers.Add("X-Response", "yes");
            return response;
        }
    }
}
