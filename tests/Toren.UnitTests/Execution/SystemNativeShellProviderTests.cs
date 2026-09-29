using NUnit.Framework;
using Toren.Platform.Execution.Adapters;

namespace Toren.UnitTests.Execution;

[TestFixture]
public sealed class SystemNativeShellProviderTests
{
    [Test]
    public void CreateShellRequestUsesHostShellAndWorkingDirectory()
    {
        var provider = new SystemNativeShellProvider();
        var workingDirectory = Path.GetTempPath();

        var request = provider.CreateShellRequest(workingDirectory);

        var configuredShell = OperatingSystem.IsWindows()
            ? Environment.GetEnvironmentVariable("COMSPEC")
            : Environment.GetEnvironmentVariable("SHELL");
        var expectedShell = string.IsNullOrWhiteSpace(configuredShell)
            ? OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh"
            : configuredShell;

        Assert.Multiple(() =>
        {
            Assert.That(request.FileName, Is.EqualTo(expectedShell));
            Assert.That(request.WorkingDirectory, Is.EqualTo(Path.GetFullPath(workingDirectory)));
            Assert.That(request.Arguments, Is.Empty);
        });
    }
}
