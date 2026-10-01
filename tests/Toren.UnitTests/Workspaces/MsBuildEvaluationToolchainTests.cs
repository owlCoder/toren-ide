using System.Text.Json;
using NUnit.Framework;
using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.Platform.Execution.Adapters;
using Toren.Workspaces.Adapters;
using Toren.Workspaces.Models;

namespace Toren.UnitTests.Workspaces;

/// <summary>
/// Runs the real .NET SDK. The injected collection targets cannot be exercised with fakes, so
/// these tests pin the shared invocation to the per-project queries it replaces.
/// </summary>
[TestFixture]
[Category("Toolchain")]
public sealed class MsBuildEvaluationToolchainTests
{
    private static readonly string TargetFramework = $"net{Environment.Version.Major}.0";

    private string _root = string.Empty;
    private string _app = string.Empty;
    private string _library = string.Empty;
    private string _unrestored = string.Empty;
    private string _spaced = string.Empty;

    [OneTimeSetUp]
    public async Task CreateWorkspace()
    {
        _root = Path.Combine(Path.GetTempPath(), $"toren-toolchain-{Guid.NewGuid():N}");
        _library = WriteProject("Library", "Library.csproj", $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{TargetFramework}</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>
              <ItemGroup>
                <AdditionalFiles Include="analysis.json" />
              </ItemGroup>
            </Project>
            """, "Widget.cs", "namespace Library; public sealed class Widget { }");
        // Packages change items from initial targets, which run before any target that is built.
        // Plain evaluation must not see that; the design-time stage must.
        await File.WriteAllTextAsync(Path.Combine(_root, "Directory.Build.targets"), """
            <Project InitialTargets="AddGeneratedEntryPoint">
              <Target Name="AddGeneratedEntryPoint">
                <ItemGroup>
                  <Compile Include="$(MSBuildThisFileDirectory)Shared/GeneratedEntryPoint.cs" />
                </ItemGroup>
              </Target>
            </Project>
            """);
        // Inputs that single-targeted projects already see during plain evaluation.
        await File.WriteAllTextAsync(Path.Combine(_root, ".editorconfig"), "root = true\n[*.cs]\nindent_size = 4\n");
        await File.WriteAllTextAsync(Path.Combine(_root, "Library", "analysis.json"), "{}");
        // TargetFrameworks makes this an outer, cross-targeting build even with a single framework.
        _app = WriteProject("App", "App.csproj", $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>{TargetFramework}</TargetFrameworks>
                <OutputType>Exe</OutputType>
                <ImplicitUsings>enable</ImplicitUsings>
                <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
                <DefineConstants>$(DefineConstants);TOREN_TOOLCHAIN_TEST</DefineConstants>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="../Library/Library.csproj" />
                <Using Include="System.Math" Static="true" />
                <Using Include="System.Text.StringBuilder" Alias="Text" />
                <Compile Include="../Shared/Linked.cs" Link="Linked.cs" />
              </ItemGroup>
            </Project>
            """, "Program.cs", "System.Console.WriteLine(new Library.Widget());");
        _unrestored = WriteProject("Unrestored", "Unrestored.csproj", $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{TargetFramework}</TargetFramework>
                <IsTestProject>true</IsTestProject>
              </PropertyGroup>
            </Project>
            """, "Tests.cs", "public sealed class Tests { }");
        _spaced = WriteProject("Spaced (dir) & more", "Spaced Project.csproj", $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{TargetFramework}</TargetFramework>
              </PropertyGroup>
            </Project>
            """, "Type.cs", "public sealed class Type { }");
        Directory.CreateDirectory(Path.Combine(_root, "Shared"));
        await File.WriteAllTextAsync(Path.Combine(_root, "Shared", "Linked.cs"), "public sealed class Linked { }");

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var restore = await new SystemProcessRunner().RunAsync(
            ProcessRequest.Create("dotnet", "restore", _app, "-nologo", "-verbosity:quiet"), timeout.Token);
        Assert.That(restore.IsSuccess && restore.Value!.Succeeded, Is.True,
            restore.IsSuccess ? restore.Value!.StandardOutput + restore.Value.StandardError : restore.Error.Message);
    }

    [OneTimeTearDown]
    public void DeleteWorkspace()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Test]
    public async Task SharedInvocationDescribesProjectsExactlyLikePerProjectQueries()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var perProjectRunner = new CountingRunner();
        var perProject = new MsBuildProjectEvaluationProvider(perProjectRunner);
        var batchRunner = new CountingRunner();
        var fallbackRunner = new CountingRunner();
        var batch = new MsBuildBatchProjectEvaluationProvider(batchRunner, new MsBuildProjectEvaluationProvider(fallbackRunner));
        string[] paths = [_app, _library, _unrestored, _spaced];

        var expected = await perProject.EvaluateAsync(paths, timeout.Token);
        var actual = await batch.EvaluateAsync(paths, timeout.Token);

        Assert.That(expected.IsSuccess, Is.True, expected.Error.Message);
        Assert.That(actual.IsSuccess, Is.True, actual.Error.Message);
        Assert.Multiple(() =>
        {
            Assert.That(JsonSerializer.Serialize(actual.Value), Is.EqualTo(JsonSerializer.Serialize(expected.Value)));
            Assert.That(perProjectRunner.Calls, Is.EqualTo(paths.Length));
            Assert.That(batchRunner.Calls, Is.EqualTo(1));
            Assert.That(fallbackRunner.Calls, Is.Zero);

            var app = actual.Value![0];
            Assert.That(app.Metadata.TargetFrameworks, Is.EqualTo(new[] { TargetFramework }));
            Assert.That(app.Metadata.OutputType, Is.EqualTo("Exe"));
            Assert.That(app.Metadata.AllowUnsafe, Is.True);
            Assert.That(app.Metadata.ProjectAssetsFilePath,
                Is.EqualTo(Path.Combine(_root, "App", "obj", "project.assets.json")));
            Assert.That(app.References.Single(reference => reference.Kind == ProjectReferenceKind.Project).ResolvedPath,
                Is.EqualTo(_library));
            Assert.That(actual.Value[2].Metadata.IsTestProject, Is.True);
            Assert.That(actual.Value[1].Metadata.SourcePaths, Does.Contain(Path.Combine(_root, "Library", "Widget.cs")));
            Assert.That(actual.Value.SelectMany(evaluation => evaluation.Metadata.SourcePaths).Select(Path.GetFileName),
                Has.None.EqualTo("GeneratedEntryPoint.cs"));
        });

        var projects = paths
            .Select((path, index) => new WorkspaceProject(path, Path.GetFileNameWithoutExtension(path), expected.Value![index].Metadata, expected.Value[index].References))
            .ToArray();
        var expectedInputs = await perProject.ResolveCompilerInputsAsync(projects, timeout.Token);
        var actualInputs = await batch.ResolveCompilerInputsAsync(projects, timeout.Token);

        Assert.That(expectedInputs.IsSuccess, Is.True, expectedInputs.Error.Message);
        Assert.That(actualInputs.IsSuccess, Is.True, actualInputs.Error.Message);
        Assert.Multiple(() =>
        {
            for (var index = 0; index < paths.Length; index++)
            {
                var expectedMetadata = expectedInputs.Value![index];
                var actualMetadata = actualInputs.Value![index];
                Assert.That(
                    JsonSerializer.Serialize(actualMetadata with { CompilerInputsError = default }),
                    Is.EqualTo(JsonSerializer.Serialize(expectedMetadata with { CompilerInputsError = default })),
                    paths[index]);
                Assert.That(actualMetadata.CompilerInputsError.Code, Is.EqualTo(expectedMetadata.CompilerInputsError.Code), paths[index]);
            }

            Assert.That(batchRunner.Calls, Is.EqualTo(2));
            Assert.That(fallbackRunner.Calls, Is.Zero);

            var app = actualInputs.Value![0];
            Assert.That(app.CompilerInputsError.IsNone, Is.True, app.CompilerInputsError.Message);
            Assert.That(app.ReferencePaths!.Select(Path.GetFileName), Does.Contain("System.Runtime.dll"));
            Assert.That(app.ReferencePaths!.Select(Path.GetFileName), Does.Contain("Library.dll"));
            Assert.That(app.SourcePaths, Does.Contain(Path.Combine(_root, "App", "Program.cs")));
            Assert.That(app.SourcePaths, Does.Contain(Path.Combine(_root, "Shared", "Linked.cs")));
            Assert.That(app.SourcePaths, Does.Contain(Path.Combine(_root, "Shared", "GeneratedEntryPoint.cs")));
            Assert.That(app.SourcePaths.Select(Path.GetFileName), Has.Some.EndsWith(".GlobalUsings.g.cs"));
            Assert.That(app.GlobalUsings, Does.Contain("global using static System.Math;"));
            Assert.That(app.GlobalUsings, Does.Contain("global using Text = System.Text.StringBuilder;"));
            Assert.That(app.DefineConstants, Does.Contain("TOREN_TOOLCHAIN_TEST"));
            Assert.That(app.AnalyzerPaths, Is.Not.Empty);
            Assert.That(app.AnalyzerConfigPaths, Does.Contain(Path.Combine(_root, ".editorconfig")));
            Assert.That(actualInputs.Value[1].AdditionalFilePaths,
                Is.EqualTo(new[] { Path.Combine(_root, "Library", "analysis.json") }));

            // A project that was never restored is reported individually, with MSBuild's own error.
            var unrestored = actualInputs.Value[2];
            Assert.That(unrestored.ReferencePaths, Is.Null);
            Assert.That(unrestored.CompilerInputsError.Message, Does.Contain("NETSDK1004"));
            Assert.That(unrestored.CompilerInputsError.Message,
                Is.EqualTo(expectedInputs.Value![2].CompilerInputsError.Message));
            Assert.That(unrestored.IsTestProject, Is.True);
        });
    }

    [Test]
    public async Task ProjectThatCannotBeLoadedFailsWithThePerProjectError()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var malformed = WriteProject("Malformed", "Malformed.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>", "Type.cs", string.Empty);
        var perProject = new MsBuildProjectEvaluationProvider(new SystemProcessRunner());
        var batch = new MsBuildBatchProjectEvaluationProvider(new SystemProcessRunner(), perProject);

        var expected = await perProject.EvaluateAsync([_library, malformed], timeout.Token);
        var actual = await batch.EvaluateAsync([_library, malformed], timeout.Token);

        Assert.Multiple(() =>
        {
            Assert.That(expected.IsFailure, Is.True);
            Assert.That(actual.IsFailure, Is.True);
            Assert.That(actual.Error, Is.EqualTo(expected.Error));
            Assert.That(actual.Error.Message, Does.Contain("Malformed.csproj"));
        });
    }

    private string WriteProject(string directory, string projectFile, string project, string sourceFile, string source)
    {
        var projectDirectory = Path.Combine(_root, directory);
        Directory.CreateDirectory(projectDirectory);
        var projectPath = Path.Combine(projectDirectory, projectFile);
        File.WriteAllText(projectPath, project);
        File.WriteAllText(Path.Combine(projectDirectory, sourceFile), source);
        return projectPath;
    }

    private sealed class CountingRunner : IProcessRunner
    {
        private readonly SystemProcessRunner _inner = new();
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public Task<Result<ProcessResult>> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);
            return _inner.RunAsync(request, cancellationToken);
        }
    }
}
