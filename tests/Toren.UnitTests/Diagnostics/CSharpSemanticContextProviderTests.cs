using NUnit.Framework;
using Toren.App.Documents.Contracts;
using Toren.App.Documents.Models;
using Toren.App.Editor.Services;
using Toren.Core.Results;
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
