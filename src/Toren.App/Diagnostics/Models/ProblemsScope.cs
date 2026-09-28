namespace Toren.App.Diagnostics.Models;

public enum ProblemsScope
{
    Workspace,
    Project,
    CurrentDocument,
}

public sealed record ProblemsScopeContext(
    string? CurrentDocumentPath,
    string? ProjectDisplayName,
    IReadOnlyList<string> ProjectFilePaths,
    string? CurrentProjectPath = null)
{
    public static ProblemsScopeContext Empty { get; } = new(null, null, []);
}

public sealed record ProblemsProjectScope(
    string ProjectPath,
    string DisplayName,
    IReadOnlyList<string> FilePaths);

public sealed record ProblemsWorkspaceScopeIndex(IReadOnlyList<ProblemsProjectScope> Projects)
{
    public static ProblemsWorkspaceScopeIndex Empty { get; } = new([]);
}
