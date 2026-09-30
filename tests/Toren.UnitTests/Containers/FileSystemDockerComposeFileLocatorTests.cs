using NUnit.Framework;
using Toren.Containers.Services;

namespace Toren.UnitTests.Containers;

[TestFixture]
public sealed class FileSystemDockerComposeFileLocatorTests
{
    [Test]
    public void FindPrefersComposeYamlAtWorkspaceRoot()
    {
        using var directory = new TemporaryDirectory();
        var composePath = Path.Combine(directory.Path, "compose.yaml");
        File.WriteAllText(composePath, "services: {}\n");
        File.WriteAllText(Path.Combine(directory.Path, "docker-compose.yml"), "services: {}\n");
        var locator = new FileSystemDockerComposeFileLocator();

        var result = locator.Find(directory.Path);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(composePath));
        });
    }

    [Test]
    public void FindUsesWorkspaceFileDirectory()
    {
        using var directory = new TemporaryDirectory();
        var workspaceFile = Path.Combine(directory.Path, "Toren.slnx");
        var composePath = Path.Combine(directory.Path, "docker-compose.yaml");
        File.WriteAllText(workspaceFile, "<Solution />");
        File.WriteAllText(composePath, "services: {}\n");
        var locator = new FileSystemDockerComposeFileLocator();

        var result = locator.Find(workspaceFile);

        Assert.That(result.Value, Is.EqualTo(composePath));
    }

    [Test]
    public void FindReturnsNullWhenComposeFileIsAbsent()
    {
        using var directory = new TemporaryDirectory();
        var locator = new FileSystemDockerComposeFileLocator();

        var result = locator.Find(directory.Path);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.Null);
        });
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"toren-compose-locator-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
