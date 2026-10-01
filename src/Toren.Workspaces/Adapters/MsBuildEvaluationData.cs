using Toren.Core.IO;
using Toren.Workspaces.Models;

namespace Toren.Workspaces.Adapters;

/// <summary>
/// Properties and items of one evaluated project, independent of how MSBuild reported them.
/// Every evaluation adapter builds project models from this, so they cannot drift apart.
/// </summary>
internal sealed class MsBuildEvaluationData
{
    public const string ReferencePathItem = "ReferencePath";

    /// <summary>Item groups whose members are files.</summary>
    public static readonly string[] PathItems =
        ["Analyzer", "Compile", "AdditionalFiles", "EditorConfigFiles", ReferencePathItem];

    public static readonly (string ItemName, ProjectReferenceKind Kind)[] ReferenceItems =
    [
        ("ProjectReference", ProjectReferenceKind.Project),
        ("PackageReference", ProjectReferenceKind.Package),
        ("FrameworkReference", ProjectReferenceKind.Framework),
    ];

    private readonly Dictionary<string, string> _properties = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _itemPaths = new(StringComparer.Ordinal);
    private readonly List<string> _globalUsings = [];
    private readonly List<(ProjectReferenceKind Kind, string Identity)> _references = [];

    public void SetProperty(string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            _properties[name] = value.Trim();
        }
    }

    /// <summary>Records that MSBuild reported the item group, even when it has no members.</summary>
    public void DeclareItems(string itemName)
    {
        if (!_itemPaths.ContainsKey(itemName))
        {
            _itemPaths.Add(itemName, []);
        }
    }

    public void AddItemPath(string itemName, string? path)
    {
        DeclareItems(itemName);
        if (!string.IsNullOrWhiteSpace(path))
        {
            _itemPaths[itemName].Add(path.Trim());
        }
    }

    public void AddGlobalUsing(string? identity, bool isStatic, string? alias)
    {
        if (string.IsNullOrWhiteSpace(identity))
        {
            return;
        }

        var prefix = isStatic ? "static " : string.Empty;
        var assignment = string.IsNullOrWhiteSpace(alias) ? string.Empty : alias.Trim() + " = ";
        _globalUsings.Add($"global using {prefix}{assignment}{identity.Trim()};");
    }

    public void AddReference(ProjectReferenceKind kind, string? identity)
    {
        if (!string.IsNullOrWhiteSpace(identity))
        {
            _references.Add((kind, identity));
        }
    }

    public bool HasItems(string itemName) => _itemPaths.ContainsKey(itemName);

    public ProjectMetadata CreateMetadata(string projectDirectory)
    {
        var targetFrameworks = GetProperty("TargetFrameworks") ?? GetProperty("TargetFramework");
        var frameworks = targetFrameworks is null
            ? []
            : targetFrameworks
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        var generatedEditorConfig = GetProperty("GeneratedMSBuildEditorConfigFile");
        var projectAssetsFile = GetProperty("ProjectAssetsFile");

        return new ProjectMetadata(
            frameworks,
            GetProperty("OutputType"),
            GetProperty("AssemblyName"),
            GetProperty("RootNamespace"),
            GetBoolean("IsTestProject"),
            GetBoolean("ManagePackageVersionsCentrally"),
            GetProperty("DirectoryBuildPropsPath"),
            GetProperty("DirectoryBuildTargetsPath"),
            GetProperty("DirectoryPackagesPropsPath"))
        {
            AnalyzerPaths = GetOrderedPaths("Analyzer", projectDirectory),
            SourcePaths = GetOrderedPaths("Compile", projectDirectory),
            GlobalUsings = _globalUsings.ToArray(),
            DefineConstants = (GetProperty("DefineConstants") ?? string.Empty)
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            Nullable = GetProperty("Nullable"),
            LanguageVersion = GetProperty("LangVersion"),
            AllowUnsafe = GetBoolean("AllowUnsafeBlocks"),
            AdditionalFilePaths = GetOrderedPaths("AdditionalFiles", projectDirectory),
            AnalyzerConfigPaths = GetOrderedPaths("EditorConfigFiles", projectDirectory)
                .Concat(generatedEditorConfig is null ? [] : [NormalizePath(generatedEditorConfig, projectDirectory)])
                .Distinct(FileSystemPath.Comparer)
                .ToArray(),
            ProjectAssetsFilePath = projectAssetsFile is null
                ? null
                : NormalizePath(projectAssetsFile, projectDirectory),
        };
    }

    public IReadOnlyList<ProjectReferenceInfo> CreateReferences(string projectDirectory)
    {
        var references = new ProjectReferenceInfo[_references.Count];
        for (var index = 0; index < references.Length; index++)
        {
            var (kind, identity) = _references[index];
            references[index] = new ProjectReferenceInfo(
                identity,
                kind,
                kind == ProjectReferenceKind.Project ? NormalizePath(identity, projectDirectory) : null);
        }

        return references;
    }

    /// <summary>Returns the distinct, ordered full paths of an item group.</summary>
    public string[] GetOrderedPaths(string itemName, string projectDirectory)
    {
        if (!_itemPaths.TryGetValue(itemName, out var itemPaths) || itemPaths.Count == 0)
        {
            return [];
        }

        var paths = new HashSet<string>(itemPaths.Count, FileSystemPath.Comparer);
        foreach (var path in itemPaths)
        {
            paths.Add(NormalizePath(path, projectDirectory));
        }

        var ordered = paths.ToArray();
        Array.Sort(ordered, FileSystemPath.Comparer);
        return ordered;
    }

    public static string GetProjectDirectory(string projectPath) =>
        Path.GetDirectoryName(Path.GetFullPath(projectPath))
            ?? throw new InvalidOperationException("Project has no directory.");

    private string? GetProperty(string name) => _properties.GetValueOrDefault(name);

    private bool GetBoolean(string name) => bool.TryParse(GetProperty(name), out var value) && value;

    private static string NormalizePath(string path, string projectDirectory)
    {
        // MSBuild items can carry either separator regardless of the host OS.
        var normalizedPath = path
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.IsPathRooted(normalizedPath)
            ? normalizedPath
            : Path.Combine(projectDirectory, normalizedPath));
    }
}
