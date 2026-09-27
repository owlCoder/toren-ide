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
    string? DirectoryPackagesPropsPath);
