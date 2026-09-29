using NUnit.Framework;
using Toren.DotNet.Execution.Models;
using Toren.DotNet.Execution.Services;
using Toren.UnitTests.TestDoubles;

namespace Toren.UnitTests.DotNet;

[TestFixture]
public sealed class DotNetLaunchProfileCommandTests
{
    [Test]
    public async Task RunAddsExplicitLaunchProfileAfterFrameworkSelection()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var runner = new FakeProcessRunner("running");
            var service = new DotNetCommandService(runner);

            var result = await service.ExecuteAsync(
                new DotNetCommandRequest(
                    DotNetCommandKind.Run,
                    directory,
                    "App.csproj",
                    Configuration: "Debug",
                    TargetFramework: "net10.0",
                    LaunchProfile: "https"));

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(
                    string.Join("|", runner.LastRequest!.Arguments),
                    Is.EqualTo("run|--project|App.csproj|--configuration|Debug|--framework|net10.0|--launch-profile|https"));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task NonRunCommandsIgnoreLaunchProfile()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var runner = new FakeProcessRunner("built");
            var service = new DotNetCommandService(runner);

            var result = await service.ExecuteAsync(
                new DotNetCommandRequest(
                    DotNetCommandKind.Build,
                    directory,
                    "App.csproj",
                    LaunchProfile: "https"));

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(string.Join("|", runner.LastRequest!.Arguments), Is.EqualTo("build|App.csproj"));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"toren-launch-command-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
