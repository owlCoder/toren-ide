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
            "{\"Properties\":{\"TargetFramework\":\"\",\"TargetFrameworks\":\"net8.0;net10.0\",\"OutputType\":\"Exe\",\"AssemblyName\":\"ParcelBox.Api\",\"RootNamespace\":\"ParcelBox.Api\",\"IsTestProject\":\"false\",\"ManagePackageVersionsCentrally\":\"true\",\"DirectoryBuildPropsPath\":\"/repo/Directory.Build.props\",\"DirectoryBuildTargetsPath\":\"/repo/Directory.Build.targets\",\"DirectoryPackagesPropsPath\":\"/repo/Directory.Packages.props\"},\"Items\":{\"Analyzer\":[{\"Identity\":\"ParcelBox.Analyzers.dll\",\"FullPath\":\"/repo/.nuget/analyzers/ParcelBox.Analyzers.dll\"}]}}");
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
            Assert.That(result.Value.AnalyzerPaths, Has.Count.EqualTo(1));
            Assert.That(Path.GetFileName(result.Value.AnalyzerPaths[0]), Is.EqualTo("ParcelBox.Analyzers.dll"));
            Assert.That(
                string.Join("|", runner.LastRequest!.Arguments),
                Does.Contain("-getProperty:TargetFramework,TargetFrameworks,OutputType,AssemblyName,RootNamespace,IsTestProject,ManagePackageVersionsCentrally,DirectoryBuildPropsPath,DirectoryBuildTargetsPath,DirectoryPackagesPropsPath"));
            Assert.That(string.Join("|", runner.LastRequest.Arguments), Does.Contain("-getItem:Analyzer"));
        });
    }

    [Test]
    public async Task ReadsCompilerInputsIncludingGlobalUsingsAndCompilationOptions()
    {
        var runner = new FakeProcessRunner("""
            {"Properties":{"TargetFramework":"net10.0","OutputType":"Exe","Nullable":"enable","LangVersion":"14.0","DefineConstants":"DEBUG;NET10_0","AllowUnsafeBlocks":"true"},
             "Items":{"Compile":[{"FullPath":"/repo/Program.cs"}],"Using":[{"Identity":"System"},{"Identity":"System.Math","Static":"true"},{"Identity":"System.String","Alias":"Text"}]}}
            """);
        var result = await new MsBuildProjectMetadataProvider(runner).GetMetadataAsync("/repo/App.csproj");
        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value!.SourcePaths, Is.EqualTo(new[] { Path.GetFullPath("/repo/Program.cs") }));
            Assert.That(string.Join("|", result.Value.GlobalUsings), Is.EqualTo("global using System;|global using static System.Math;|global using Text = System.String;"));
            Assert.That(result.Value.Nullable, Is.EqualTo("enable"));
            Assert.That(result.Value.LanguageVersion, Is.EqualTo("14.0"));
            Assert.That(string.Join("|", result.Value.DefineConstants), Is.EqualTo("DEBUG|NET10_0"));
            Assert.That(result.Value.AllowUnsafe, Is.True);
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
            Assert.That(result.Value.AnalyzerPaths, Is.Empty);
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
