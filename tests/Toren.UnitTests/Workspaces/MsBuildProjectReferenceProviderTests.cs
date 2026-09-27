using NUnit.Framework;
using Toren.UnitTests.TestDoubles;
using Toren.Workspaces.Adapters;
using Toren.Workspaces.Models;

namespace Toren.UnitTests.Workspaces;

[TestFixture]
public sealed class MsBuildProjectReferenceProviderTests
{
    [Test]
    public async Task ParsesEvaluatedReferenceItemsAndResolvesProjectPath()
    {
        var project = Path.Combine(Path.GetTempPath(), "App.csproj");
        var runner = new FakeProcessRunner(
            "{\"Items\":{\"ProjectReference\":[{\"Identity\":\"../Lib/ParcelBox.Application.csproj\"}],\"PackageReference\":[{\"Identity\":\"NUnit\"}],\"FrameworkReference\":[{\"Identity\":\"Microsoft.AspNetCore.App\"}]}}");
        var provider = new MsBuildProjectReferenceProvider(runner);

        var result = await provider.GetReferencesAsync(project);

        var expectedProjectReference = Path.GetFullPath(
            Path.Combine(Path.GetTempPath(), "..", "Lib", "ParcelBox.Application.csproj"));
        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(
                string.Join(",", result.Value!.Select(reference => $"{reference.Kind}:{reference.Identity}")),
                Is.EqualTo("Project:../Lib/ParcelBox.Application.csproj,Package:NUnit,Framework:Microsoft.AspNetCore.App"));
            Assert.That(result.Value[0].ResolvedPath, Is.EqualTo(expectedProjectReference));
            Assert.That(result.Value[1].ResolvedPath, Is.Null);
            Assert.That(result.Value[2].ResolvedPath, Is.Null);
            Assert.That(
                string.Join("|", runner.LastRequest!.Arguments),
                Is.EqualTo($"msbuild|{project}|-nologo|-verbosity:quiet|-getItem:ProjectReference,PackageReference,FrameworkReference"));
        });
    }

    [Test]
    public async Task ReportsEvaluationFailure()
    {
        var runner = new FakeProcessRunner(string.Empty, exitCode: 1, standardError: "evaluation failed");
        var provider = new MsBuildProjectReferenceProvider(runner);

        var result = await provider.GetReferencesAsync(Path.Combine(Path.GetTempPath(), "Broken.csproj"));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("workspace.project.evaluate.failed"));
            Assert.That(result.Error.Message, Does.Contain("evaluation failed"));
        });
    }

    [Test]
    public async Task ReportsMalformedEvaluationData()
    {
        var provider = new MsBuildProjectReferenceProvider(new FakeProcessRunner("not-json"));

        var result = await provider.GetReferencesAsync(Path.Combine(Path.GetTempPath(), "Broken.csproj"));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("workspace.project.evaluate.failed"));
            Assert.That(result.Error.Message, Does.Contain("invalid evaluation data"));
        });
    }
}
