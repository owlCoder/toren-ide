using NUnit.Framework;
using Toren.Debugging.Adapters;

namespace Toren.UnitTests.Debugging;

[TestFixture]
public sealed class NetCoreDbgAdapterLocatorTests
{
    [Test]
    public void LocateUsesConfiguredPathBeforeSearchPath()
    {
        using var workspace = new TemporaryDirectory();
        var configured = workspace.CreateFile("configured-netcoredbg");
        var pathDirectory = workspace.CreateDirectory("bin");
        _ = workspace.CreateFile(Path.Combine("bin", "netcoredbg"));
        var locator = new NetCoreDbgAdapterLocator(
            configured,
            pathDirectory,
            isWindows: false);

        var result = locator.Locate();

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value!.FileName, Is.EqualTo(Path.GetFullPath(configured)));
            Assert.That(result.Value.Arguments, Is.EqualTo(new[] { "--interpreter=vscode" }));
            Assert.That(result.Value.DisplayName, Is.EqualTo("netcoredbg"));
        });
    }

    [Test]
    public void LocateSearchesPathForPlatformExecutable()
    {
        using var workspace = new TemporaryDirectory();
        var first = workspace.CreateDirectory("first");
        var second = workspace.CreateDirectory("second");
        var executable = workspace.CreateFile(Path.Combine("second", "netcoredbg"));
        var locator = new NetCoreDbgAdapterLocator(
            configuredPath: null,
            string.Join(Path.PathSeparator, first, second),
            isWindows: false);

        var result = locator.Locate();

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.FileName, Is.EqualTo(Path.GetFullPath(executable)));
    }

    [Test]
    public void LocateFailsClearlyWhenConfiguredPathDoesNotExist()
    {
        using var workspace = new TemporaryDirectory();
        var missing = Path.Combine(workspace.RootPath, "missing-netcoredbg");
        var locator = new NetCoreDbgAdapterLocator(
            missing,
            searchPath: null,
            isWindows: false);

        var result = locator.Locate();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("debug.adapter.configured-path-not-found"));
            Assert.That(result.Error.Message, Does.Contain(NetCoreDbgAdapterLocator.ConfiguredPathEnvironmentVariable));
        });
    }

    [Test]
    public void LocateReturnsActionableNotFoundFailure()
    {
        var locator = new NetCoreDbgAdapterLocator(
            configuredPath: null,
            searchPath: null,
            isWindows: false);

        var result = locator.Locate();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("debug.adapter.not-found"));
            Assert.That(result.Error.Message, Does.Contain("PATH"));
            Assert.That(result.Error.Message, Does.Contain(NetCoreDbgAdapterLocator.ConfiguredPathEnvironmentVariable));
        });
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            RootPath = Path.Combine(
                Path.GetTempPath(),
                $"toren-netcoredbg-locator-{Guid.NewGuid():N}");
            Directory.CreateDirectory(RootPath);
        }

        public string RootPath { get; }

        public string CreateDirectory(string relativePath)
        {
            var path = Path.Combine(RootPath, relativePath);
            Directory.CreateDirectory(path);
            return path;
        }

        public string CreateFile(string relativePath)
        {
            var path = Path.Combine(RootPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, string.Empty);
            return path;
        }

        public void Dispose()
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }
}
