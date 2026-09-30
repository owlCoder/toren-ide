namespace Toren.Workspaces.Models;

public sealed record ProjectMetadata(
    IReadOnlyList<string> TargetFrameworks,
    string? OutputType,
    string? AssemblyName,
    string? RootNamespace,
    bool IsTestProject,
    bool UsesCentralPackageManagement,
    string? DirectoryBuildPropsPath,
    string? DirectoryBuildTargetsPath,
    string? DirectoryPackagesPropsPath)
{
    public IReadOnlyList<string> SourcePaths { get; init; } = [];

    public IReadOnlyList<string> GlobalUsings { get; init; } = [];

    public IReadOnlyList<string> DefineConstants { get; init; } = [];

    public string? Nullable { get; init; }

    public string? LanguageVersion { get; init; }

    public bool AllowUnsafe { get; init; }

    public IReadOnlyList<string> AdditionalFilePaths { get; init; } = [];

    public IReadOnlyList<string> AnalyzerConfigPaths { get; init; } = [];

    public IReadOnlyList<string> AnalyzerPaths { get; init; } = [];
}
