using NUnit.Framework;
using Toren.DotNet.Environment.Services;
using Toren.UnitTests.TestDoubles;

namespace Toren.UnitTests.DotNet;

[TestFixture]
public sealed class DotNetSdkResolverTests
{
    [Test]
    public async Task ResolvesSdkInWorkspaceDirectory()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var runner = new FakeProcessRunner("10.0.100\n");
            var resolver = new DotNetSdkResolver(runner);

            var result = await resolver.ResolveVersionAsync(directory);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Value, Is.EqualTo("10.0.100"));
                Assert.That(runner.LastRequest!.WorkingDirectory, Is.EqualTo(directory));
                Assert.That(string.Join("|", runner.LastRequest.Arguments), Is.EqualTo("--version"));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task RejectsUnavailableWorkspaceDirectory()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"toren-missing-sdk-{Guid.NewGuid():N}");
        var resolver = new DotNetSdkResolver(new FakeProcessRunner("10.0.100"));

        var result = await resolver.ResolveVersionAsync(missing);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("dotnet.sdk.resolve.directory-unavailable"));
        });
    }

    [Test]
    public async Task ReportsSdkResolutionFailure()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var resolver = new DotNetSdkResolver(
                new FakeProcessRunner(string.Empty, exitCode: 1, standardError: "global.json requires 9.9.999"));

            var result = await resolver.ResolveVersionAsync(directory);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsFailure, Is.True);
                Assert.That(result.Error.Code, Is.EqualTo("dotnet.sdk.resolve.failed"));
                Assert.That(result.Error.Message, Does.Contain("global.json requires 9.9.999"));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"toren-sdk-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
