using NUnit.Framework;
using System.Text.Json;
using Toren.UnitTests.TestDoubles;
using Toren.Workspaces.Adapters;

namespace Toren.UnitTests.Workspaces;

[TestFixture]
public sealed class MsBuildProjectCompilationReferenceProviderTests
{
    [Test]
    public async Task ResolvesCompilerReferencePathsForSelectedTargetFramework()
    {
        var referencePaths = new[] { typeof(object).Assembly.Location, typeof(FakeProcessRunner).Assembly.Location };
        var runner = new FakeProcessRunner(JsonSerializer.Serialize(new
        {
            Items = new { ReferencePath = referencePaths.Select(path => new { FullPath = path }).ToArray() }
        }));
        var provider = new MsBuildProjectCompilationReferenceProvider(runner);

        var result = await provider.GetReferencePathsAsync("/repo/src/App/App.csproj", "net10.0");
        var paths = result.Value ?? throw new AssertionException("Expected resolved compilation reference paths.");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(paths, Has.Count.EqualTo(2));
            Assert.That(paths, Is.EquivalentTo(referencePaths));
            Assert.That(string.Join("|", runner.LastRequest!.Arguments), Does.Contain("-target:ResolveReferences"));
            Assert.That(string.Join("|", runner.LastRequest.Arguments), Does.Contain("-property:BuildProjectReferences=false"));
            Assert.That(string.Join("|", runner.LastRequest.Arguments), Does.Contain("-property:TargetFramework=net10.0"));
            Assert.That(string.Join("|", runner.LastRequest.Arguments), Does.Contain("-getItem:ReferencePath"));
        });
    }

    [Test]
    public async Task MissingProjectReferenceIsReportedInsteadOfCreatingFalseSemanticErrors()
    {
        var path = Path.Combine(Path.GetTempPath(), $"toren-missing-reference-{Guid.NewGuid():N}.dll");
        var provider = new MsBuildProjectCompilationReferenceProvider(new FakeProcessRunner(JsonSerializer.Serialize(new
        {
            Items = new { ReferencePath = new[] { new { FullPath = path } } }
        })));

        var result = await provider.GetReferencePathsAsync("/repo/App.csproj");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Message, Does.Contain(path));
            Assert.That(result.Error.Message, Does.Contain("Build the solution"));
        });
    }

    [Test]
    public async Task ReportsReferenceResolutionFailure()
    {
        var provider = new MsBuildProjectCompilationReferenceProvider(
            new FakeProcessRunner(string.Empty, exitCode: 1, standardError: "assets file not found"));

        var result = await provider.GetReferencePathsAsync("/repo/App.csproj", "net10.0");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("workspace.project.compilation-references.resolve.failed"));
            Assert.That(result.Error.Message, Does.Contain("assets file not found"));
        });
    }

    [Test]
    public async Task ReportsInvalidReferenceOutput()
    {
        var provider = new MsBuildProjectCompilationReferenceProvider(new FakeProcessRunner("{}"));

        var result = await provider.GetReferencePathsAsync("/repo/App.csproj");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("workspace.project.compilation-references.resolve.failed"));
            Assert.That(result.Error.Message, Does.Contain("ReferencePath"));
        });
    }
}
