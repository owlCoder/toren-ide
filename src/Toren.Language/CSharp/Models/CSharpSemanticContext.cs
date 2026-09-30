namespace Toren.Language.CSharp.Models;

public sealed record CSharpSemanticContext(
    string ActiveDocumentPath,
    IReadOnlyList<CSharpSourceDocument> Documents)
{
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
