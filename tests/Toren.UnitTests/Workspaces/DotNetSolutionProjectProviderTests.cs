using NUnit.Framework;
using Toren.UnitTests.TestDoubles;
using Toren.Workspaces.Adapters;

namespace Toren.UnitTests.Workspaces;

[TestFixture]
public sealed class DotNetSolutionProjectProviderTests
{
    [Test]
    public async Task ListsAndNormalizesProjectPaths()
    {
        var root = Path.Combine(Path.GetTempPath(), $"toren-solution-{Guid.NewGuid():N}");
        var solution = Path.Combine(root, "ParcelBox.sln");
        var runner = new FakeProcessRunner(
            "Project(s)\n----------\nsrc/ParcelBox.Api/ParcelBox.Api.csproj\nsrc\\ParcelBox.Application\\ParcelBox.Application.csproj\n");
        var provider = new DotNetSolutionProjectProvider(runner);
        var expectedPaths = string.Join(
            "|",
            Path.GetFullPath(Path.Combine(root, "src", "ParcelBox.Api", "ParcelBox.Api.csproj")),
            Path.GetFullPath(Path.Combine(root, "src", "ParcelBox.Application", "ParcelBox.Application.csproj")));

        var result = await provider.GetProjectPathsAsync(solution);

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(string.Join("|", result.Value!), Is.EqualTo(expectedPaths));
            Assert.That(string.Join("|", runner.LastRequest!.Arguments), Is.EqualTo($"sln|{solution}|list"));
        });
    }

    [Test]
    public async Task ReportsDotNetCommandFailure()
    {
        var runner = new FakeProcessRunner(string.Empty, exitCode: 1, standardError: "solution failed");
        var provider = new DotNetSolutionProjectProvider(runner);

        var result = await provider.GetProjectPathsAsync(Path.Combine(Path.GetTempPath(), "Broken.sln"));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("workspace.solution.list.failed"));
            Assert.That(result.Error.Message, Does.Contain("solution failed"));
        });
    }
}
