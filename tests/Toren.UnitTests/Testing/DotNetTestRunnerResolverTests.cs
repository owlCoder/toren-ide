using NUnit.Framework;
using Toren.DotNet.Testing.Models;
using Toren.DotNet.Testing.Services;

namespace Toren.UnitTests.Testing;

[TestFixture]
public sealed class DotNetTestRunnerResolverTests
{
    [Test]
    public void DefaultsToVSTestWithoutGlobalJson()
    {
        using var workspace = new TemporaryWorkspace();
        var projectPath = workspace.CreateProject("tests/Sample.Tests/Sample.Tests.csproj");

        Assert.That(DotNetTestRunnerResolver.Resolve(projectPath), Is.EqualTo(DotNetTestRunner.VSTest));
    }

    [Test]
    public void ResolvesMicrosoftTestingPlatformFromNearestGlobalJson()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.Write("global.json", """
            {
              "test": {
                "runner": "VSTest"
              }
            }
            """);
        workspace.Write("tests/global.json", """
            {
              "test": {
                "runner": "Microsoft.Testing.Platform"
              }
            }
            """);
        var projectPath = workspace.CreateProject("tests/Sample.Tests/Sample.Tests.csproj");

        Assert.That(
            DotNetTestRunnerResolver.Resolve(projectPath),
            Is.EqualTo(DotNetTestRunner.MicrosoftTestingPlatform));
    }

    [Test]
    public void InvalidGlobalJsonFallsBackToVSTestAndLeavesSdkAuthoritative()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.Write("global.json", "{ invalid json");
        var projectPath = workspace.CreateProject("Sample.Tests/Sample.Tests.csproj");

        Assert.That(DotNetTestRunnerResolver.Resolve(projectPath), Is.EqualTo(DotNetTestRunner.VSTest));
    }

    private sealed class TemporaryWorkspace : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            $"toren-test-runner-{Guid.NewGuid():N}");

        public TemporaryWorkspace()
        {
            Directory.CreateDirectory(_root);
        }

        public string CreateProject(string relativePath)
        {
            Write(relativePath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            return Path.GetFullPath(Path.Combine(_root, relativePath));
        }

        public void Write(string relativePath, string content)
        {
            var path = Path.Combine(_root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        public void Dispose()
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
