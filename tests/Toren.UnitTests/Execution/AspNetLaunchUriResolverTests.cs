using NUnit.Framework;
using Toren.App.Execution.Services;
using Toren.DotNet.Execution.Models;

namespace Toren.UnitTests.Execution;

[TestFixture]
public sealed class AspNetLaunchUriResolverTests
{
    [Test]
    public void ResolvesLaunchUrlFromListeningAddress()
    {
        var resolver = new AspNetLaunchUriResolver();
        var profile = CreateProfile("swagger");

        var uri = resolver.Resolve(profile, "info: Microsoft.Hosting.Lifetime[14] Now listening on: https://localhost:7240");

        Assert.That(uri, Is.EqualTo(new Uri("https://localhost:7240/swagger")));
    }

    [Test]
    public void UsesListeningAddressWhenLaunchUrlIsMissing()
    {
        var resolver = new AspNetLaunchUriResolver();
        var profile = CreateProfile(null);

        var uri = resolver.Resolve(profile, "Now listening on: http://localhost:5050");

        Assert.That(uri, Is.EqualTo(new Uri("http://localhost:5050")));
    }

    [TestCase("Application started. Press Ctrl+C to shut down.")]
    [TestCase("Now listening on: ftp://localhost:2121")]
    public void IgnoresNonHttpListeningOutput(string output)
    {
        var resolver = new AspNetLaunchUriResolver();

        var uri = resolver.Resolve(CreateProfile("swagger"), output);

        Assert.That(uri, Is.Null);
    }

    private static DotNetLaunchProfile CreateProfile(string? launchUrl) =>
        new(
            "https",
            true,
            launchUrl,
            "https://localhost:7240;http://localhost:5240",
            new Dictionary<string, string>());
}
