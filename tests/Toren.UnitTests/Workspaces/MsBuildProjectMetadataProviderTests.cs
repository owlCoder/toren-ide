using NUnit.Framework;
using Toren.UnitTests.TestDoubles;
using Toren.Workspaces.Adapters;

namespace Toren.UnitTests.Workspaces;

[TestFixture]
public sealed class MsBuildProjectMetadataProviderTests
{
    [Test]
    public async Task ReadsEvaluatedProjectMetadata()
    {
        var runner = new FakeProcessRunner(
            "{\"Properties\":{\"TargetFramework\":\"\",\"TargetFrameworks\":\"net8.0;net10.0\",\"OutputType\":\"Exe\",\"AssemblyName\":\"ParcelBox.Api\",\"RootNamespace\":\"ParcelBox.Api\",\"IsTestProject\":\"false\",\"ManagePackageVersionsCentrally\":\"true\",\"DirectoryBuildPropsPath\":\"/repo/Directory.Build.props\",\"DirectoryBuildTargetsPath\":\"/repo/Directory.Build.targets\",\"DirectoryPackagesPropsPath\":\"/repo/Directory.Packages.props\"}}");
        var provider = new MsBuildProjectMetadataProvider(runner);

        var result = await provider.GetMetadataAsync("/repo/src/ParcelBox.Api/ParcelBox.Api.csproj");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(string.Join("|", result.Value!.TargetFrameworks), Is.EqualTo("net8.0|net10.0"));
            Assert.That(result.Value.OutputType, Is.EqualTo("Exe"));
            Assert.That(result.Value.AssemblyName, Is.EqualTo("ParcelBox.Api"));
            Assert.That(result.Value.RootNamespace, Is.EqualTo("ParcelBox.Api"));
            Assert.That(result.Value.IsTestProject, Is.False);
            Assert.That(result.Value.UsesCentralPackageManagement, Is.True);
            Assert.That(result.Value.DirectoryBuildPropsPath, Is.EqualTo("/repo/Directory.Build.props"));
            Assert.That(result.Value.DirectoryBuildTargetsPath, Is.EqualTo("/repo/Directory.Build.targets"));
            Assert.That(result.Value.DirectoryPackagesPropsPath, Is.EqualTo("/repo/Directory.Packages.props"));
            Assert.That(
                string.Join("|", runner.LastRequest!.Arguments),
                Does.Contain("-getProperty:TargetFramework,TargetFrameworks,OutputType,AssemblyName,RootNamespace,IsTestProject,ManagePackageVersionsCentrally,DirectoryBuildPropsPath,DirectoryBuildTargetsPath,DirectoryPackagesPropsPath"));
        });
    }

    [Test]
    public async Task UsesSingleTargetFrameworkWhenMultiTargetPropertyIsEmpty()
    {
        var runner = new FakeProcessRunner(
            "{\"Properties\":{\"TargetFramework\":\"net10.0\",\"TargetFrameworks\":\"\",\"IsTestProject\":\"true\"}}");
        var provider = new MsBuildProjectMetadataProvider(runner);

        var result = await provider.GetMetadataAsync("/repo/tests/ParcelBox.Tests/ParcelBox.Tests.csproj");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(string.Join("|", result.Value!.TargetFrameworks), Is.EqualTo("net10.0"));
            Assert.That(result.Value.IsTestProject, Is.True);
            Assert.That(result.Value.UsesCentralPackageManagement, Is.False);
        });
    }

    [Test]
    public async Task ReportsInvalidEvaluationOutput()
    {
        var provider = new MsBuildProjectMetadataProvider(new FakeProcessRunner("not-json"));

        var result = await provider.GetMetadataAsync("/repo/App.csproj");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("workspace.project.metadata.evaluate.failed"));
            Assert.That(result.Error.Message, Does.Contain("invalid evaluation data"));
        });
    }

    [Test]
    public async Task ReportsMsBuildEvaluationFailure()
    {
        var provider = new MsBuildProjectMetadataProvider(
            new FakeProcessRunner(string.Empty, exitCode: 1, standardError: "evaluation failed"));

        var result = await provider.GetMetadataAsync("/repo/App.csproj");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("workspace.project.metadata.evaluate.failed"));
            Assert.That(result.Error.Message, Does.Contain("evaluation failed"));
        });
    }
}
