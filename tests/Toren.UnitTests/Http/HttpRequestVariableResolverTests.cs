using NUnit.Framework;
using Toren.DotNet.Http.Models;
using Toren.DotNet.Http.Services;

namespace Toren.UnitTests.Http;

[TestFixture]
public sealed class HttpRequestVariableResolverTests
{
    [Test]
    public void ResolveSubstitutesUriHeadersAndBody()
    {
        var request = new HttpRequestDefinition(
            "POST",
            new Uri("https://example.test/{{tenant}}/widgets"),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Authorization"] = "Bearer {{token}}",
                ["X-Environment"] = "{{environment}}",
            },
            "{\"name\":\"{{name}}\"}",
            "createWidget");
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["tenant"] = "contoso",
            ["token"] = "secret-token",
            ["environment"] = "development",
            ["name"] = "demo",
        };

        var result = new HttpRequestVariableResolver().Resolve(request, variables);

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value!.Uri.AbsoluteUri, Is.EqualTo("https://example.test/contoso/widgets"));
            Assert.That(result.Value.Headers["Authorization"], Is.EqualTo("Bearer secret-token"));
            Assert.That(result.Value.Headers["X-Environment"], Is.EqualTo("development"));
            Assert.That(result.Value.Body, Is.EqualTo("{\"name\":\"demo\"}"));
            Assert.That(result.Value.Name, Is.EqualTo("createWidget"));
        });
    }

    [Test]
    public void ResolveReturnsExplicitFailureForMissingVariable()
    {
        var request = CreateRequest("https://example.test/{{tenant}}/widgets");

        var result = new HttpRequestVariableResolver().Resolve(
            request,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("http.variable.missing"));
            Assert.That(result.Error.Message, Does.Contain("tenant"));
        });
    }

    [Test]
    public void ResolveRejectsMalformedPlaceholder()
    {
        var request = new HttpRequestDefinition(
            "POST",
            new Uri("https://example.test/widgets"),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            "{{token");

        var result = new HttpRequestVariableResolver().Resolve(
            request,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("http.variable.placeholder.invalid"));
        });
    }

    [Test]
    public void ResolveAllowsRelativePathVariableValuesWithinHttpUri()
    {
        var request = CreateRequest("https://example.test/{{path}}");
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["path"] = "api/widgets",
        };

        var result = new HttpRequestVariableResolver().Resolve(request, variables);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.Uri.AbsoluteUri, Is.EqualTo("https://example.test/api/widgets"));
    }

    private static HttpRequestDefinition CreateRequest(string uri) =>
        new(
            "GET",
            new Uri(uri),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
}
