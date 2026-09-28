using NUnit.Framework;
using Toren.App.Diagnostics.Services;
using Toren.App.Editor.Contracts;
using Toren.App.Editor.Models;
using Toren.Core.Results;
using Toren.Language.CSharp.Contracts;
using Toren.Language.CSharp.Models;

namespace Toren.UnitTests.Diagnostics;

[TestFixture]
public sealed class WorkspaceDiagnosticsCoordinatorTests
{
    [Test]
    public async Task ScanCombinesProjectAwareAndLooseFileDiagnostics()
    {
        var alphaPath = Path.GetFullPath("Alpha.cs");
        var betaPath = Path.GetFullPath("Beta.cs");
        var loosePath = Path.GetFullPath("Loose.cs");
        var projectDocuments = new[]
        {
            new CSharpSourceDocument(alphaPath, "namespace Demo; public sealed class Alpha { }"),
            new CSharpSourceDocument(betaPath, "namespace Demo; public sealed class Beta { }"),
        };
        var projectContext = new CSharpWorkspaceProjectContext(
            new CSharpSemanticContext(alphaPath, projectDocuments),
            [alphaPath, betaPath]);
        var semanticProvider = new FakeSemanticContextProvider(
            new CSharpWorkspaceSemanticContexts(
                [projectContext],
                [new CSharpSourceDocument(loosePath, "public sealed class Loose")])) ;
        var workspaceService = new FakeWorkspaceDiagnosticService(betaPath);
        var syntaxService = new FakeSyntaxService();
        using var coordinator = new WorkspaceDiagnosticsCoordinator(
            semanticProvider,
            workspaceService,
            syntaxService);

        var result = await coordinator.AnalyzeLatestAsync(Path.GetFullPath("workspace"), []);

        Assert.That(result, Is.Not.Null);
        var documentDiagnostics = result!.DocumentDiagnostics;
        Assert.Multiple(() =>
        {
            Assert.That(documentDiagnostics, Has.Count.EqualTo(3));
            Assert.That(result.WorkspaceDiagnostics, Is.Empty);
            Assert.That(workspaceService.CallCount, Is.EqualTo(1));
            Assert.That(syntaxService.CallCount, Is.EqualTo(1));
            Assert.That(documentDiagnostics.Single(item => item.FilePath == alphaPath).Diagnostics, Is.Empty);
            Assert.That(
                documentDiagnostics.Single(item => item.FilePath == betaPath).Diagnostics.Single().Id,
                Is.EqualTo("CS0103"));
            Assert.That(
                documentDiagnostics.Single(item => item.FilePath == loosePath).Diagnostics.Single().Id,
                Is.EqualTo("CS1002"));
        });
    }

    [Test]
    public async Task ProjectSystemFailureIsSurfacedAlongsideSyntaxFallback()
    {
        var loosePath = Path.GetFullPath("Loose.cs");
        var projectSystemError = OperationError.Create(
            "workspace.project.metadata.evaluate.failed",
            "Could not evaluate project metadata: restore assets are unavailable.");
        var semanticProvider = new FakeSemanticContextProvider(
            new CSharpWorkspaceSemanticContexts(
                [],
                [new CSharpSourceDocument(loosePath, "public sealed class Loose")],
                projectSystemError));
        var workspaceService = new FakeWorkspaceDiagnosticService(loosePath);
        var syntaxService = new FakeSyntaxService();
        using var coordinator = new WorkspaceDiagnosticsCoordinator(
            semanticProvider,
            workspaceService,
            syntaxService);

        var result = await coordinator.AnalyzeLatestAsync(Path.GetFullPath("workspace"), []);

        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result!.DocumentDiagnostics, Has.Count.EqualTo(1));
            Assert.That(result.WorkspaceDiagnostics, Has.Count.EqualTo(1));
            Assert.That(result.WorkspaceDiagnostics[0].Code, Is.EqualTo(projectSystemError.Code));
            Assert.That(result.WorkspaceDiagnostics[0].Message, Is.EqualTo(projectSystemError.Message));
            Assert.That(result.WorkspaceDiagnostics[0].Source, Is.EqualTo("Project system"));
            Assert.That(syntaxService.CallCount, Is.EqualTo(1));
            Assert.That(workspaceService.CallCount, Is.Zero);
        });
    }

    private sealed class FakeSemanticContextProvider(CSharpWorkspaceSemanticContexts contexts)
        : ICSharpSemanticContextProvider
    {
        public Task<CSharpSemanticContext?> CreateAsync(
            string workspacePath,
            CSharpSourceDocument activeDocument,
            IReadOnlyList<CSharpSourceDocument> openDocuments,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CSharpSemanticContext?> CreateWorkspaceAsync(
            string workspacePath,
            IReadOnlyList<CSharpSourceDocument> openDocuments,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CSharpWorkspaceSemanticContexts?> CreateWorkspaceProjectContextsAsync(
            string workspacePath,
            IReadOnlyList<CSharpSourceDocument> openDocuments,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<CSharpWorkspaceSemanticContexts?>(contexts);
        }
    }

    private sealed class FakeWorkspaceDiagnosticService(string diagnosticPath)
        : ICSharpWorkspaceDiagnosticService
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<CSharpDocumentDiagnostics>> AnalyzeDocumentsAsync(
            CSharpSemanticContext context,
            IReadOnlyList<string> documentPaths,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            IReadOnlyList<CSharpDocumentDiagnostics> result = documentPaths
                .Select(path => new CSharpDocumentDiagnostics(
                    path,
                    path == diagnosticPath
                        ? [new CSharpDiagnostic(
                            "CS0103",
                            "The name does not exist in the current context",
                            CSharpDiagnosticSeverity.Error,
                            1,
                            1,
                            1,
                            4)]
                        : []))
                .ToArray();
            return Task.FromResult(result);
        }
    }

    private sealed class FakeSyntaxService : ICSharpSyntaxService
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<CSharpDiagnostic>> AnalyzeAsync(
            string sourceText,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult<IReadOnlyList<CSharpDiagnostic>>(
                [new CSharpDiagnostic(
                    "CS1002",
                    "; expected",
                    CSharpDiagnosticSeverity.Error,
                    1,
                    1,
                    1,
                    1)]);
        }
    }
}
