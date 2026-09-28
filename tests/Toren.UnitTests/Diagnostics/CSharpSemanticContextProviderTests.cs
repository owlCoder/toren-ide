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
            var referencePath = Path.Combine(root, "refs", "Demo.Package.dll");
            const string source = "public sealed class Program { }";
            var graph = CreateGraph(projectPath);
            var referenceProvider = new StubCompilationReferenceProvider([referencePath]);
            var provider = new CSharpSemanticContextProvider(
                new WorkspaceClassifier(),
                new SuccessfulProjectGraphService(graph),
                new StubWorkspaceFileProvider(
                    [new WorkspaceFileEntry(sourcePath, "Program.cs", "Program.cs")]),
                new StubDocumentStore(sourcePath, source),
                referenceProvider);

            var result = await provider.CreateAsync(
                root,
                new CSharpSourceDocument(sourcePath, source),
                []);

            Assert.That(result, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(result!.MetadataReferencePaths, Is.EqualTo([Path.GetFullPath(referencePath)]));
                Assert.That(referenceProvider.LastProjectPath, Is.EqualTo(Path.GetFullPath(projectPath)));
                Assert.That(referenceProvider.LastTargetFramework, Is.EqualTo("net10.0"));
            });
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
                new SuccessfulProjectGraphService(CreateGraph(projectPath)),
                new StubWorkspaceFileProvider(
                    [new WorkspaceFileEntry(sourcePath, "Program.cs", "Program.cs")]),
                new StubDocumentStore(sourcePath, "public sealed class Program { }"),
                new FailingCompilationReferenceProvider(error));

            var result = await provider.CreateWorkspaceProjectContextsAsync(root, []);

            Assert.That(result, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(result!.ProjectContexts, Is.Empty);
                Assert.That(result.LooseDocuments.Select(document => document.Path),
                    Is.EqualTo([Path.GetFullPath(sourcePath)]));
                Assert.That(result.ProjectSystemError, Is.EqualTo(error));
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static WorkspaceProjectGraph CreateGraph(string projectPath)
    {
        var project = new WorkspaceProject(
            Path.GetFullPath(projectPath),
            "App",
            new ProjectMetadata(
                ["net10.0"],
                "Library",
                "App",
                "App",
                false,
                false,
                null,
                null,
                null),
            []);
        return new WorkspaceProjectGraph([project]);
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

    private sealed class StubCompilationReferenceProvider(IReadOnlyList<string> paths)
        : IProjectCompilationReferenceProvider
    {
        public string? LastProjectPath { get; private set; }

        public string? LastTargetFramework { get; private set; }

        public Task<Result<IReadOnlyList<string>>> GetReferencePathsAsync(
            string projectPath,
            string? targetFramework = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastProjectPath = Path.GetFullPath(projectPath);
            LastTargetFramework = targetFramework;
            return Task.FromResult(Result.Success(paths));
        }
    }

    private sealed class FailingCompilationReferenceProvider(OperationError error)
        : IProjectCompilationReferenceProvider
    {
        public Task<Result<IReadOnlyList<string>>> GetReferencePathsAsync(
            string projectPath,
            string? targetFramework = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Result.Failure<IReadOnlyList<string>>(error));
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
