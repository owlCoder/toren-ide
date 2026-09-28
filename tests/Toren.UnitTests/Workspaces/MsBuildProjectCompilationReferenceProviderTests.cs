using NUnit.Framework;
using Toren.UnitTests.TestDoubles;
using Toren.Workspaces.Adapters;

namespace Toren.UnitTests.Workspaces;

[TestFixture]
public sealed class MsBuildProjectCompilationReferenceProviderTests
{
    [Test]
    public async Task ResolvesCompilerReferencePathsForSelectedTargetFramework()
    {
        var runner = new FakeProcessRunner(
            "{\"Items\":{\"ReferencePath\":[{\"Identity\":\"Demo.Package.dll\",\"FullPath\":\"/repo/.nuget/packages/demo/1.0.0/lib/net10.0/Demo.Package.dll\"},{\"Identity\":\"System.Runtime.dll\",\"FullPath\":\"/dotnet/packs/Microsoft.NETCore.App.Ref/10.0.0/ref/net10.0/System.Runtime.dll\"}]}}");
        var provider = new MsBuildProjectCompilationReferenceProvider(runner);

        var result = await provider.GetReferencePathsAsync("/repo/src/App/App.csproj", "net10.0");
        var paths = result.Value ?? throw new AssertionException("Expected resolved compilation reference paths.");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(paths, Has.Count.EqualTo(2));
            Assert.That(paths.Select(Path.GetFileName), Does.Contain("Demo.Package.dll"));
            Assert.That(paths.Select(Path.GetFileName), Does.Contain("System.Runtime.dll"));
            Assert.That(string.Join("|", runner.LastRequest!.Arguments), Does.Contain("-target:ResolveReferences"));
            Assert.That(string.Join("|", runner.LastRequest.Arguments), Does.Contain("-property:BuildProjectReferences=false"));
            Assert.That(string.Join("|", runner.LastRequest.Arguments), Does.Contain("-property:TargetFramework=net10.0"));
            Assert.That(string.Join("|", runner.LastRequest.Arguments), Does.Contain("-getItem:ReferencePath"));
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
