using Toren.App.Documents.Contracts;
using Toren.App.Editor.Contracts;
using Toren.Language.CSharp.Models;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Models;

namespace Toren.App.Editor.Services;

public sealed class CSharpSemanticContextProvider(
    IWorkspaceClassifier workspaceClassifier,
    IWorkspaceProjectGraphService projectGraphService,
    IWorkspaceFileProvider workspaceFileProvider,
    ITextDocumentStore documentStore) : ICSharpSemanticContextProvider
{
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private readonly IWorkspaceClassifier _workspaceClassifier = workspaceClassifier
        ?? throw new ArgumentNullException(nameof(workspaceClassifier));
    private readonly IWorkspaceProjectGraphService _projectGraphService = projectGraphService
        ?? throw new ArgumentNullException(nameof(projectGraphService));
    private readonly IWorkspaceFileProvider _workspaceFileProvider = workspaceFileProvider
        ?? throw new ArgumentNullException(nameof(workspaceFileProvider));
    private readonly ITextDocumentStore _documentStore = documentStore
        ?? throw new ArgumentNullException(nameof(documentStore));

    public async Task<CSharpSemanticContext?> CreateAsync(
        string workspacePath,
        CSharpSourceDocument activeDocument,
        IReadOnlyList<CSharpSourceDocument> openDocuments,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        ArgumentNullException.ThrowIfNull(activeDocument);
        ArgumentNullException.ThrowIfNull(openDocuments);
        cancellationToken.ThrowIfCancellationRequested();

        if (!Path.GetExtension(activeDocument.Path).Equals(".cs", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var workspace = ClassifyWorkspace(workspacePath);
        if (workspace is null)
        {
            return null;
        }

        var graphResult = await _projectGraphService.LoadAsync(workspace, cancellationToken).ConfigureAwait(false);
        if (!graphResult.IsSuccess || graphResult.Value.Projects.Count == 0)
        {
            return null;
        }

        var projectDirectories = CreateProjectDirectories(graphResult.Value.Projects);
        var activePath = Path.GetFullPath(activeDocument.Path);
        var activeProject = FindOwningProject(activePath, projectDirectories);
        if (activeProject is null)
        {
            return null;
        }

        var reachableProjects = GetReachableProjectPaths(activeProject, graphResult.Value.Projects);
        var sourceDocuments = await LoadSourceDocumentsAsync(
            workspacePath,
            openDocuments,
            fullPath =>
            {
                var owner = FindOwningProject(fullPath, projectDirectories);
                return owner is not null && reachableProjects.Contains(Path.GetFullPath(owner.Path));
            },
            cancellationToken).ConfigureAwait(false);
        if (sourceDocuments is null)
        {
            return null;
        }

        if (!sourceDocuments.Any(document => PathComparer.Equals(document.Path, activePath)))
        {
            sourceDocuments.Add(new CSharpSourceDocument(activePath, activeDocument.Text));
        }

        return new CSharpSemanticContext(activePath, sourceDocuments)
        {
            AnalyzerPaths = GetAnalyzerPaths(reachableProjects, graphResult.Value.Projects),
        };
    }

    public async Task<CSharpSemanticContext?> CreateWorkspaceAsync(
        string workspacePath,
        IReadOnlyList<CSharpSourceDocument> openDocuments,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        ArgumentNullException.ThrowIfNull(openDocuments);
        cancellationToken.ThrowIfCancellationRequested();

        var sourceDocuments = await LoadSourceDocumentsAsync(
            workspacePath,
            openDocuments,
            static _ => true,
            cancellationToken).ConfigureAwait(false);
        if (sourceDocuments is null || sourceDocuments.Count == 0)
        {
            return null;
        }

        var preferredActivePath = openDocuments
            .Select(document => Path.GetFullPath(document.Path))
            .FirstOrDefault(path => sourceDocuments.Any(document => PathComparer.Equals(document.Path, path)));
        var activePath = preferredActivePath ?? sourceDocuments[0].Path;
        return new CSharpSemanticContext(activePath, sourceDocuments);
    }

    private async Task<List<CSharpSourceDocument>?> LoadSourceDocumentsAsync(
        string workspacePath,
        IReadOnlyList<CSharpSourceDocument> openDocuments,
        Func<string, bool> includeFile,
        CancellationToken cancellationToken)
    {
        var filesResult = await _workspaceFileProvider.GetFilesAsync(workspacePath, cancellationToken).ConfigureAwait(false);
        if (!filesResult.IsSuccess)
        {
            return null;
        }

        var openDocumentText = openDocuments.ToDictionary(
            document => Path.GetFullPath(document.Path),
            document => document.Text,
            PathComparer);
        var sourceDocuments = new List<CSharpSourceDocument>();

        foreach (var file in filesResult.Value)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Path.GetExtension(file.Path).Equals(".cs", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(file.Path);
            if (!includeFile(fullPath))
            {
                continue;
            }

            if (openDocumentText.TryGetValue(fullPath, out var openText))
            {
                sourceDocuments.Add(new CSharpSourceDocument(fullPath, openText));
                continue;
            }

            var loaded = await _documentStore.LoadAsync(fullPath, cancellationToken).ConfigureAwait(false);
            if (loaded.IsSuccess)
            {
                sourceDocuments.Add(new CSharpSourceDocument(fullPath, loaded.Value.Text));
            }
        }

        return sourceDocuments;
    }

    private WorkspaceDescriptor? ClassifyWorkspace(string workspacePath)
    {
        if (Directory.Exists(workspacePath))
        {
            return _workspaceClassifier.ClassifyDirectory(workspacePath);
        }

        return _workspaceClassifier.TryClassifyFile(workspacePath, out var workspace)
            ? workspace
            : null;
    }

    private static ProjectDirectory[] CreateProjectDirectories(IReadOnlyList<WorkspaceProject> projects) =>
        projects
            .Select(project => new ProjectDirectory(
                project,
                Path.GetDirectoryName(Path.GetFullPath(project.Path))
                    ?? Directory.GetCurrentDirectory()))
            .ToArray();

    private static WorkspaceProject? FindOwningProject(
        string filePath,
        IReadOnlyList<ProjectDirectory> projectDirectories)
    {
        return projectDirectories
            .Where(project => IsWithinDirectory(filePath, project.Directory))
            .OrderByDescending(project => project.Directory.Length)
            .Select(project => project.Project)
            .FirstOrDefault();
    }

    private static HashSet<string> GetReachableProjectPaths(
        WorkspaceProject activeProject,
        IReadOnlyList<WorkspaceProject> projects)
    {
        var projectsByPath = projects.ToDictionary(
            project => Path.GetFullPath(project.Path),
            project => project,
            PathComparer);
        var reachable = new HashSet<string>(PathComparer);
        var pending = new Queue<string>();
        pending.Enqueue(Path.GetFullPath(activeProject.Path));

        while (pending.Count > 0)
        {
            var projectPath = pending.Dequeue();
            if (!reachable.Add(projectPath) || !projectsByPath.TryGetValue(projectPath, out var project))
            {
                continue;
            }

            foreach (var reference in project.References)
            {
                if (reference.Kind != ProjectReferenceKind.Project || string.IsNullOrWhiteSpace(reference.ResolvedPath))
                {
                    continue;
                }

                var referencedProjectPath = Path.GetFullPath(reference.ResolvedPath);
                if (projectsByPath.ContainsKey(referencedProjectPath) && !reachable.Contains(referencedProjectPath))
                {
                    pending.Enqueue(referencedProjectPath);
                }
            }
        }

        return reachable;
    }

    private static string[] GetAnalyzerPaths(
        HashSet<string> reachableProjectPaths,
        IReadOnlyList<WorkspaceProject> projects) =>
        projects
            .Where(project => reachableProjectPaths.Contains(Path.GetFullPath(project.Path)))
            .SelectMany(project => project.Metadata.AnalyzerPaths)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(PathComparer)
            .OrderBy(static path => path, PathComparer)
            .ToArray();

    private static bool IsWithinDirectory(string filePath, string directoryPath)
    {
        var relativePath = Path.GetRelativePath(directoryPath, filePath);
        if (relativePath.Equals("..", StringComparison.Ordinal))
        {
            return false;
        }

        var parentPrefix = $"..{Path.DirectorySeparatorChar}";
        return !relativePath.StartsWith(parentPrefix, StringComparison.Ordinal)
            && !Path.IsPathRooted(relativePath);
    }

    private sealed record ProjectDirectory(WorkspaceProject Project, string Directory);
}
