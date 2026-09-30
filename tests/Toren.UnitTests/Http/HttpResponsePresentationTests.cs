using NUnit.Framework;
using Toren.DotNet.Http.Models;
using Toren.DotNet.Http.Services;

namespace Toren.UnitTests.Http;

[TestFixture]
public sealed class HttpResponsePresentationTests
{
    [Test]
    public void FormatBodyPrettyPrintsJsonResponse()
    {
        var response = CreateResponse(
            "{\"name\":\"toren\",\"count\":2}",
            "application/json; charset=utf-8");

        var formatted = new HttpResponseFormatter().FormatBody(response);

        Assert.Multiple(() =>
        {
            Assert.That(formatted, Does.Contain(System.Environment.NewLine));
            Assert.That(formatted, Does.Contain("\"name\": \"toren\""));
            Assert.That(formatted, Does.Contain("\"count\": 2"));
        });
    }

    [Test]
    public void FormatBodyPreservesInvalidJsonAndNonJsonContent()
    {
        var formatter = new HttpResponseFormatter();
        var invalidJson = formatter.FormatBody(CreateResponse("{not-json}", "application/json"));
        var plainText = formatter.FormatBody(CreateResponse("hello", "text/plain"));

        Assert.Multiple(() =>
        {
            Assert.That(invalidJson, Is.EqualTo("{not-json}"));
            Assert.That(plainText, Is.EqualTo("hello"));
        });
    }

    [Test]
    public void HistoryKeepsNewestEntriesWithinCapacityAndCanClear()
    {
        var history = new HttpRequestHistory(capacity: 2);
        var response = CreateResponse("ok", "text/plain");
        var first = CreateRequest("first", "https://example.test/first");
        var second = CreateRequest("second", "https://example.test/second");
        var third = CreateRequest("third", "https://example.test/third");
        var now = new DateTimeOffset(2026, 9, 30, 4, 30, 0, TimeSpan.Zero);

        history.Record(first, response, now);
        history.Record(second, response, now.AddSeconds(1));
        history.Record(third, response, now.AddSeconds(2));

        Assert.Multiple(() =>
        {
            Assert.That(history.Entries, Has.Count.EqualTo(2));
            Assert.That(history.Entries[0].Name, Is.EqualTo("third"));
            Assert.That(history.Entries[1].Name, Is.EqualTo("second"));
            Assert.That(history.Entries[0].StatusCode, Is.EqualTo(200));
        });

        history.Clear();
        Assert.That(history.Entries, Is.Empty);
    }

    private static HttpResponseSnapshot CreateResponse(string body, string contentType)
    {
        var headers = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Content-Type"] = new List<string> { contentType },
        };
        return new HttpResponseSnapshot(200, "OK", headers, body, TimeSpan.FromMilliseconds(25));
    }

    private static HttpRequestDefinition CreateRequest(string name, string uri) =>
        new(
            "GET",
            new Uri(uri),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Name: name);
}
