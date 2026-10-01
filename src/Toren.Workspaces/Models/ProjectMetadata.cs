using Toren.Core.Results;

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

    /// <summary>The NuGet assets file whose content determines restored package inputs.</summary>
    public string? ProjectAssetsFilePath { get; init; }

    /// <summary>
    /// Compiler reference assemblies resolved by the design-time targets, or <see langword="null"/>
    /// when those targets have not produced them.
    /// </summary>
    public IReadOnlyList<string>? ReferencePaths { get; init; }

    /// <summary>
    /// Why design-time compiler inputs are unavailable, for example because the project is not
    /// restored. The remaining metadata then comes from evaluation alone.
    /// </summary>
    public OperationError CompilerInputsError { get; init; } = OperationError.None;
}
