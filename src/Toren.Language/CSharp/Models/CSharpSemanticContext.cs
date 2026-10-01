namespace Toren.Language.CSharp.Models;

public sealed record CSharpSemanticContext(
    string ActiveDocumentPath,
    IReadOnlyList<CSharpSourceDocument> Documents)
{
    /// <summary>
    /// The project the documents belong to, when known. Requests for the same project reuse
    /// analysis state that is still valid for the current documents.
    /// </summary>
    public string? ProjectPath { get; init; }

    public IReadOnlyList<string> DefineConstants { get; init; } = [];

    public string? Nullable { get; init; }

    public string? LanguageVersion { get; init; }

    public string? OutputType { get; init; }

    public bool AllowUnsafe { get; init; }

    public IReadOnlyList<string> AdditionalFilePaths { get; init; } = [];

    public IReadOnlyList<string> AnalyzerConfigPaths { get; init; } = [];

    public IReadOnlyList<string> AnalyzerPaths { get; init; } = [];

    public IReadOnlyList<string> MetadataReferencePaths { get; init; } = [];
}
