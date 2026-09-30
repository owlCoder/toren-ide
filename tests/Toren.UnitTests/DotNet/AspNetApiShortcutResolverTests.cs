using NUnit.Framework;
using Toren.DotNet.AspNetCore.Models;
using Toren.DotNet.AspNetCore.Services;

namespace Toren.UnitTests.DotNet;

[TestFixture]
public sealed class AspNetApiShortcutResolverTests
{
    [Test]
    public void ResolveBuildsSwaggerAndOpenApiUrlsFromApplicationOrigin()
    {
        var resolver = new AspNetApiShortcutResolver();

        var result = resolver.Resolve(new Uri("https://localhost:7123/base/path"));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Has.Count.EqualTo(2));
            Assert.That(result.Value![0].Kind, Is.EqualTo(AspNetApiShortcutKind.SwaggerUi));
            Assert.That(result.Value[0].Uri, Is.EqualTo(new Uri("https://localhost:7123/swagger")));
            Assert.That(result.Value[1].Kind, Is.EqualTo(AspNetApiShortcutKind.OpenApiDocument));
            Assert.That(result.Value[1].Uri, Is.EqualTo(new Uri("https://localhost:7123/openapi/v1.json")));
        });
    }

    [Test]
    public void ResolveRejectsNonHttpApplicationUrl()
    {
        var resolver = new AspNetApiShortcutResolver();

        var result = resolver.Resolve(new Uri("file:///tmp/app"));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("dotnet.aspnet-api-shortcuts.base-uri.unsupported"));
        });
    }
}
