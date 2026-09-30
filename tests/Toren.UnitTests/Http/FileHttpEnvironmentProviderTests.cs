using NUnit.Framework;
using Toren.DotNet.Http.Adapters;

namespace Toren.UnitTests.Http;

[TestFixture]
public sealed class FileHttpEnvironmentProviderTests
{
    [Test]
    public async Task NearestEnvironmentFileMergesSharedNamedAndUserOverrides()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var nested = Directory.CreateDirectory(Path.Combine(root, "api", "requests")).FullName;
            await File.WriteAllTextAsync(
                Path.Combine(root, "http-client.env.json"),
                """
                {
                  "$shared": {
                    "baseUrl": "https://shared.example.test",
                    "token": "shared-token"
                  },
                  "Development": {
                    "baseUrl": "https://dev.example.test",
                    "feature": true
                  },
                  "Production": {
                    "baseUrl": "https://prod.example.test"
                  }
                }
                """);
            await File.WriteAllTextAsync(
                Path.Combine(root, "http-client.env.json.user"),
                """
                {
                  "$shared": {
                    "token": "user-token"
                  },
                  "Development": {
                    "secret": "local-secret"
                  }
                }
                """);
            var documentPath = Path.Combine(nested, "requests.http");

            var result = await new FileHttpEnvironmentProvider().GetEnvironmentsAsync(documentPath);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Has.Count.EqualTo(2));
            var development = result.Value!.Single(environment => environment.Name == "Development");
            var production = result.Value.Single(environment => environment.Name == "Production");
            Assert.Multiple(() =>
            {
                Assert.That(development.Variables["baseUrl"], Is.EqualTo("https://dev.example.test"));
                Assert.That(development.Variables["token"], Is.EqualTo("user-token"));
                Assert.That(development.Variables["feature"], Is.EqualTo("true"));
                Assert.That(development.Variables["secret"], Is.EqualTo("local-secret"));
                Assert.That(production.Variables["baseUrl"], Is.EqualTo("https://prod.example.test"));
                Assert.That(production.Variables["token"], Is.EqualTo("user-token"));
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task NearestEnvironmentFileWinsOverAncestorFile()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var nested = Directory.CreateDirectory(Path.Combine(root, "api")).FullName;
            await File.WriteAllTextAsync(
                Path.Combine(root, "http-client.env.json"),
                "{\"Root\":{\"baseUrl\":\"https://root.example.test\"}}");
            await File.WriteAllTextAsync(
                Path.Combine(nested, "http-client.env.json"),
                "{\"Nested\":{\"baseUrl\":\"https://nested.example.test\"}}");

            var result = await new FileHttpEnvironmentProvider()
                .GetEnvironmentsAsync(Path.Combine(nested, "requests.http"));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Has.Count.EqualTo(1));
            Assert.That(result.Value![0].Name, Is.EqualTo("Nested"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task MissingEnvironmentFileReturnsEmptySuccess()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var result = await new FileHttpEnvironmentProvider()
                .GetEnvironmentsAsync(Path.Combine(root, "requests.http"));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.Empty);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task InvalidEnvironmentJsonReturnsParseFailure()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "http-client.env.json"), "{ invalid json");

            var result = await new FileHttpEnvironmentProvider()
                .GetEnvironmentsAsync(Path.Combine(root, "requests.http"));

            Assert.Multiple(() =>
            {
                Assert.That(result.IsFailure, Is.True);
                Assert.That(result.Error.Code, Is.EqualTo("http.environment.parse.failed"));
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"toren-http-env-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
