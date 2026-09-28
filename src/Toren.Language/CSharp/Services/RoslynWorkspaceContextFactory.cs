using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Toren.Language.CSharp.Models;

namespace Toren.Language.CSharp.Services;

internal static class RoslynWorkspaceContextFactory
{
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    public static RoslynWorkspaceContext? Create(
        CSharpSemanticContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Documents.Count == 0)
        {
            return null;
        }

        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId("Toren.CSharp");
        var projectInfo = ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            "Toren.CSharp",
            "Toren.CSharp",
            LanguageNames.CSharp,
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
            parseOptions: new CSharpParseOptions(LanguageVersion.Preview),
            metadataReferences: RoslynMetadataReferenceProvider.GetReferences());
        var solution = workspace.CurrentSolution.AddProject(projectInfo);

        DocumentId? activeDocumentId = null;
        foreach (var sourceDocument in context.Documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var documentId = DocumentId.CreateNewId(projectId, debugName: sourceDocument.Path);
            solution = solution.AddDocument(
                documentId,
                Path.GetFileName(sourceDocument.Path),
                SourceText.From(sourceDocument.Text),
                filePath: sourceDocument.Path);
            if (sourceDocument.Path.Equals(context.ActiveDocumentPath, PathComparison))
            {
                activeDocumentId = documentId;
            }
        }

        if (activeDocumentId is null)
        {
            workspace.Dispose();
            return null;
        }

        return new RoslynWorkspaceContext(workspace, solution, activeDocumentId);
    }
}

internal sealed class RoslynWorkspaceContext(
    AdhocWorkspace workspace,
    Solution solution,
    DocumentId activeDocumentId) : IDisposable
{
    public AdhocWorkspace Workspace { get; } = workspace;

    public Solution Solution { get; } = solution;

    public DocumentId ActiveDocumentId { get; } = activeDocumentId;

    public Document ActiveDocument => Solution.GetDocument(ActiveDocumentId)
        ?? throw new InvalidOperationException("The active Roslyn document is unavailable.");

    public void Dispose()
    {
        Workspace.Dispose();
    }
}
