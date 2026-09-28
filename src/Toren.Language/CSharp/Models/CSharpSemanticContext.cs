namespace Toren.Language.CSharp.Models;

public sealed record CSharpSemanticContext(
    string ActiveDocumentPath,
    IReadOnlyList<CSharpSourceDocument> Documents)
{
    public IReadOnlyList<string> AnalyzerPaths { get; init; } = [];

    public IReadOnlyList<string> MetadataReferencePaths { get; init; } = [];
}
