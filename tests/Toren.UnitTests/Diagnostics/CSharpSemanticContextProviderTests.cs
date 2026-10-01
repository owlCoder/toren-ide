using NUnit.Framework;
using Toren.App.Documents.Contracts;
using Toren.App.Documents.Models;
using Toren.App.Editor.Services;
using Toren.Core.Results;
using Toren.Language.CSharp.Models;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Models;
using Toren.Workspaces.Services;

namespace Toren.UnitTests.Diagnostics;

[TestFixture]
public sealed class CSharpSemanticContextProviderTests
{
    [Test]
    public async Task WorkspaceProjectContextsPreserveProjectGraphFailureForProblemsPipeline()
    {
        var root = Path.Combine(Path.GetTempPath(), $"toren-semantic-context-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var sourcePath = Path.Combine(root, "Program.cs");
            var graphError = OperationError.Create(
                "workspace.project.metadata.evaluate.failed",
                "Could not evaluate project metadata: assets file is missing.");
            var provider = new CSharpSemanticContextProvider(
                new WorkspaceClassifier(),
                new FailingProjectGraphService(graphError),
                new StubWorkspaceFileProvider(
                    [new WorkspaceFileEntry(sourcePath, "Program.cs", "Program.cs")]),
                new StubDocumentStore(sourcePath, "public sealed class Program { }"));

            var result = await provider.CreateWorkspaceProjectContextsAsync(root, []);

            Assert.That(result, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(result!.ProjectContexts, Is.Empty);
                Assert.That(result.LooseDocuments, Has.Count.EqualTo(1));
                Assert.That(result.ProjectSystemError, Is.EqualTo(graphError));
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task ActiveProjectContextCarriesResolvedCompilationReferences()
    {
        var root = Path.Combine(Path.GetTempPath(), $"toren-semantic-references-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var sourcePath = Path.Combine(root, "Program.cs");
            var projectPath = Path.Combine(root, "App.csproj");
            var referencePath = Path.GetFullPath(Path.Combine(root, "refs", "Demo.Package.dll"));
            Directory.CreateDirectory(Path.GetDirectoryName(referencePath)!);
            await File.WriteAllBytesAsync(referencePath, []);
            const string source = "public sealed class Program { }";
            var graph = CreateGraph(projectPath, metadata => metadata with { ReferencePaths = [referencePath] });
            var provider = new CSharpSemanticContextProvider(
                new WorkspaceClassifier(),
                new SuccessfulProjectGraphService(graph),
                new StubWorkspaceFileProvider(
                    [new WorkspaceFileEntry(sourcePath, "Program.cs", "Program.cs")]),
                new StubDocumentStore(sourcePath, source));

            var result = await provider.CreateAsync(
                root,
                new CSharpSourceDocument(sourcePath, source),
                []);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.MetadataReferencePaths, Is.EqualTo([referencePath]));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task ReferenceResolutionFailureFallsBackToSyntaxAndSurfacesProjectSystemError()
    {
        var root = Path.Combine(Path.GetTempPath(), $"toren-semantic-reference-failure-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var sourcePath = Path.Combine(root, "Program.cs");
            var projectPath = Path.Combine(root, "App.csproj");
            var error = OperationError.Create(
                "workspace.project.compilation-references.resolve.failed",
                "Could not resolve project compilation references: assets file is missing.");
            var provider = new CSharpSemanticContextProvider(
                new WorkspaceClassifier(),
                new SuccessfulProjectGraphService(
                    CreateGraph(projectPath, metadata => metadata with { CompilerInputsError = error })),
                new StubWorkspaceFileProvider(
                    [new WorkspaceFileEntry(sourcePath, "Program.cs", "Program.cs")]),
                new StubDocumentStore(sourcePath, "public sealed class Program { }"));

            var result = await provider.CreateWorkspaceProjectContextsAsync(root, []);
            var activeContext = await provider.CreateAsync(
                root,
                new CSharpSourceDocument(sourcePath, "public sealed class Program { }"),
                []);

            Assert.That(result, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(result!.ProjectContexts, Is.Empty);
                Assert.That(result.LooseDocuments.Select(document => document.Path),
                    Is.EqualTo([Path.GetFullPath(sourcePath)]));
                Assert.That(result.ProjectSystemError, Is.EqualTo(error));
                Assert.That(activeContext, Is.Null);
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task UnbuiltProjectReferenceIsReportedUntilTheAssemblyExists()
    {
        var root = Path.Combine(Path.GetTempPath(), $"toren-semantic-unbuilt-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var sourcePath = Path.Combine(root, "Program.cs");
            var referencePath = Path.GetFullPath(Path.Combine(root, "Library.dll"));
            var provider = new CSharpSemanticContextProvider(
                new WorkspaceClassifier(),
                new SuccessfulProjectGraphService(CreateGraph(
                    Path.Combine(root, "App.csproj"),
                    metadata => metadata with { ReferencePaths = [referencePath] })),
                new StubWorkspaceFileProvider(
                    [new WorkspaceFileEntry(sourcePath, "Program.cs", "Program.cs")]),
                new StubDocumentStore(sourcePath, "public sealed class Program { }"));

            var beforeBuild = await provider.CreateWorkspaceProjectContextsAsync(root, []);
            await File.WriteAllBytesAsync(referencePath, []);
            var afterBuild = await provider.CreateWorkspaceProjectContextsAsync(root, []);

            Assert.Multiple(() =>
            {
                Assert.That(beforeBuild!.ProjectContexts, Is.Empty);
                Assert.That(beforeBuild.ProjectSystemError.Message, Does.Contain("Build the solution"));
                Assert.That(afterBuild!.ProjectSystemError.IsNone, Is.True);
                Assert.That(afterBuild.ProjectContexts, Has.Count.EqualTo(1));
                Assert.That(afterBuild.ProjectContexts[0].SemanticContext.MetadataReferencePaths,
                    Is.EqualTo([referencePath]));
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task EvaluatedProjectsCompileOnlyTheirOwnSourcesAndIgnoreOtherProjectsFailures()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"toren-semantic-projects-{Guid.NewGuid():N}"));
        var appSource = Path.Combine(root, "App", "Program.cs");
        var excludedSource = Path.Combine(root, "App", "Excluded.cs");
        var librarySource = Path.Combine(root, "Library", "Widget.cs");
        var linkedSource = Path.Combine(root, "Shared", "Linked.cs");
        var generatedSource = Path.Combine(root, "App", "obj", "App.AssemblyInfo.cs");
        var libraryError = OperationError.Create("test.unrestored", "Library is not restored.");
        var library = CreateProject(Path.Combine(root, "Library", "Library.csproj"), "Library", metadata => metadata with
        {
            SourcePaths = [librarySource, linkedSource],
            CompilerInputsError = libraryError,
        });
        var app = CreateProject(Path.Combine(root, "App", "App.csproj"), "App", metadata => metadata with
        {
            SourcePaths = [appSource, generatedSource, linkedSource],
            GlobalUsings = ["global using System;"],
            ReferencePaths = [],
        }) with
        {
            References = [new ProjectReferenceInfo("../Library/Library.csproj", ProjectReferenceKind.Project, library.Path)],
        };
        var files = new[] { appSource, excludedSource, librarySource, linkedSource }
            .Select(path => new WorkspaceFileEntry(path, Path.GetRelativePath(root, path), Path.GetFileName(path)))
            .ToArray();
        var store = new PathEchoDocumentStore();
        var provider = new CSharpSemanticContextProvider(
            new WorkspaceClassifier(),
            new SuccessfulProjectGraphService(new WorkspaceProjectGraph([library, app])),
            new StubWorkspaceFileProvider(files),
            store);
        var openApp = new CSharpSourceDocument(appSource, "// unsaved editor text");
        Directory.CreateDirectory(root);
        try
        {
            var active = await provider.CreateAsync(root, openApp, [openApp]);
            var workspace = await provider.CreateWorkspaceProjectContextsAsync(root, [openApp]);

            Assert.That(active, Is.Not.Null);
            Assert.That(workspace, Is.Not.Null);
            var appContext = workspace!.ProjectContexts.Single();
            Assert.Multiple(() =>
            {
                // The active project is analyzed even though another project cannot resolve references.
                Assert.That(active!.Documents.Select(document => document.Path),
                    Is.EquivalentTo(new[] { appSource, generatedSource, linkedSource, app.Path + ".global-usings.g.cs" }));
                Assert.That(active.Documents.Single(document => document.Path == appSource).Text,
                    Is.EqualTo("// unsaved editor text"));
                Assert.That(active.Documents.Any(document => document.Path.EndsWith("Excluded.cs", StringComparison.Ordinal)), Is.False);

                Assert.That(appContext.ProjectPath, Is.EqualTo(app.Path));
                Assert.That(appContext.DocumentPaths, Is.EqualTo(new[] { appSource, generatedSource }));
                Assert.That(appContext.SemanticContext.Documents.Select(document => document.Path),
                    Is.EquivalentTo(new[] { appSource, generatedSource, linkedSource, app.Path + ".global-usings.g.cs" }));
                Assert.That(appContext.SemanticContext.Documents.Single(document => document.Path == appSource).Text,
                    Is.EqualTo("// unsaved editor text"));
                Assert.That(workspace.ProjectSystemError, Is.EqualTo(libraryError));
                // Library sources fall back to syntax-only analysis; the linked file is owned by the first project listing it.
                Assert.That(workspace.LooseDocuments.Select(document => document.Path),
                    Is.EquivalentTo(new[] { librarySource, linkedSource }));
                // The linked file is read once and shared between the projects that compile it.
                Assert.That(store.LoadCounts[linkedSource], Is.EqualTo(2));
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task ProjectsWithoutResolvedReferencesAreAnalyzedTogetherWithReferencedProjectSources()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"toren-semantic-source-{Guid.NewGuid():N}"));
        var appSource = Path.Combine(root, "App", "Program.cs");
        var librarySource = Path.Combine(root, "Library", "Widget.cs");
        var unrelatedSource = Path.Combine(root, "Unrelated", "Other.cs");
        var library = CreateProject(Path.Combine(root, "Library", "Library.csproj"), "Library");
        var unrelated = CreateProject(Path.Combine(root, "Unrelated", "Unrelated.csproj"), "Unrelated");
        var app = CreateProject(Path.Combine(root, "App", "App.csproj"), "App") with
        {
            References = [new ProjectReferenceInfo("../Library/Library.csproj", ProjectReferenceKind.Project, library.Path)],
        };
        var files = new[] { appSource, librarySource, unrelatedSource }
            .Select(path => new WorkspaceFileEntry(path, Path.GetRelativePath(root, path), Path.GetFileName(path)))
            .ToArray();
        var provider = new CSharpSemanticContextProvider(
            new WorkspaceClassifier(),
            new SuccessfulProjectGraphService(new WorkspaceProjectGraph([app, library, unrelated])),
            new StubWorkspaceFileProvider(files),
            new PathEchoDocumentStore());
        Directory.CreateDirectory(root);
        try
        {
            var active = await provider.CreateAsync(root, new CSharpSourceDocument(appSource, "class Program { }"), []);
            var workspace = await provider.CreateWorkspaceProjectContextsAsync(root, []);

            Assert.That(active, Is.Not.Null);
            Assert.That(workspace, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(active!.Documents.Select(document => document.Path),
                    Is.EquivalentTo(new[] { appSource, librarySource }));
                Assert.That(active.MetadataReferencePaths, Is.Empty);
                Assert.That(workspace!.ProjectContexts.Select(context => context.ProjectPath),
                    Is.EqualTo(new[] { app.Path, library.Path, unrelated.Path }));
                Assert.That(workspace.ProjectContexts[0].DocumentPaths, Is.EqualTo(new[] { appSource }));
                Assert.That(workspace.ProjectContexts[0].SemanticContext.Documents.Select(document => document.Path),
                    Is.EquivalentTo(new[] { appSource, librarySource }));
                Assert.That(workspace.ProjectContexts[1].SemanticContext.Documents.Select(document => document.Path),
                    Is.EqualTo(new[] { librarySource }));
                Assert.That(workspace.LooseDocuments, Is.Empty);
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static WorkspaceProjectGraph CreateGraph(
        string projectPath,
        Func<ProjectMetadata, ProjectMetadata>? configure = null) =>
        new([CreateProject(projectPath, "App", configure)]);

    private static WorkspaceProject CreateProject(
        string projectPath,
        string name,
        Func<ProjectMetadata, ProjectMetadata>? configure = null)
    {
        var metadata = new ProjectMetadata(
            ["net10.0"],
            "Library",
            name,
            name,
            false,
            false,
            null,
            null,
            null);
        return new WorkspaceProject(Path.GetFullPath(projectPath), name, configure?.Invoke(metadata) ?? metadata, []);
    }

    private sealed class FailingProjectGraphService(OperationError error) : IWorkspaceProjectGraphService
    {
        public Task<Result<WorkspaceProjectGraph>> LoadAsync(
            WorkspaceDescriptor workspace,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Result.Failure<WorkspaceProjectGraph>(error));
        }
    }

    private sealed class SuccessfulProjectGraphService(WorkspaceProjectGraph graph) : IWorkspaceProjectGraphService
    {
        public Task<Result<WorkspaceProjectGraph>> LoadAsync(
            WorkspaceDescriptor workspace,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Result.Success(graph));
        }
    }

    private sealed class StubWorkspaceFileProvider(IReadOnlyList<WorkspaceFileEntry> files) : IWorkspaceFileProvider
    {
        public Task<Result<IReadOnlyList<WorkspaceFileEntry>>> GetFilesAsync(
            string workspacePath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Result.Success(files));
        }
    }

    private sealed class PathEchoDocumentStore : ITextDocumentStore
    {
        public System.Collections.Concurrent.ConcurrentDictionary<string, int> LoadCounts { get; } = new();

        public Task<Result<TextDocumentContent>> LoadAsync(
            string documentPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LoadCounts.AddOrUpdate(documentPath, 1, static (_, count) => count + 1);
            return Task.FromResult(Result.Success(
                new TextDocumentContent(documentPath, $"// {documentPath}", TextDocumentEncoding.Utf8)));
        }

        public Task<Result<TextDocumentContent>> SaveAsync(
            TextDocumentContent document,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubDocumentStore(string path, string text) : ITextDocumentStore
    {
        public Task<Result<TextDocumentContent>> LoadAsync(
            string documentPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Result.Success(new TextDocumentContent(path, text, TextDocumentEncoding.Utf8)));
        }

        public Task<Result<TextDocumentContent>> SaveAsync(
            TextDocumentContent document,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
