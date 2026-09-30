using Toren.App.Documents.Contracts;
using Toren.App.Editor.Contracts;
using Toren.App.Editor.Models;
using Toren.Core.Results;
using Toren.Language.CSharp.Models;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Models;
using Toren.Workspaces.Services;

namespace Toren.App.Editor.Services;

public sealed class CSharpSemanticContextProvider(
    IWorkspaceClassifier workspaceClassifier,
    IWorkspaceProjectGraphService projectGraphService,
    IWorkspaceFileProvider workspaceFileProvider,
    ITextDocumentStore documentStore,
    IProjectCompilationReferenceProvider? projectCompilationReferenceProvider = null) : ICSharpSemanticContextProvider
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
    private readonly IProjectCompilationReferenceProvider? _projectCompilationReferenceProvider =
        projectCompilationReferenceProvider;

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

        var projects = graphResult.Value.Projects;
        var projectOwnership = new WorkspaceProjectOwnershipMap(projects);
        var activePath = Path.GetFullPath(activeDocument.Path);
        var activeProject = projectOwnership.FindOwningProject(activePath);
        if (activeProject is null)
        {
            return null;
        }

        var reachableProjects = _projectCompilationReferenceProvider is null
            ? GetReachableProjectPaths(activeProject, projects)
            : new HashSet<string>([Path.GetFullPath(activeProject.Path)], PathComparer);
        var referenceResults = await ResolveProjectReferencePathsAsync(projects, cancellationToken).ConfigureAwait(false);
        var metadataReferences = CollectMetadataReferencePaths(reachableProjects, referenceResults);
        if (!metadataReferences.IsSuccess)
        {
            return null;
        }

        var sourceDocuments = await LoadSourceDocumentsAsync(
            workspacePath,
            openDocuments,
            fullPath =>
            {
                var owner = projectOwnership.FindOwningProject(fullPath);
                return owner is not null && reachableProjects.Contains(Path.GetFullPath(owner.Path))
                    && IsCompiledSource(owner, fullPath);
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

        await AddEvaluatedSourcesAsync(activeProject, sourceDocuments, openDocuments, cancellationToken).ConfigureAwait(false);
        return CreateProjectContext(activePath, sourceDocuments, activeProject, metadataReferences.Value);
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

    public async Task<CSharpWorkspaceSemanticContexts?> CreateWorkspaceProjectContextsAsync(
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
        if (sourceDocuments is null)
        {
            return null;
        }

        var workspace = ClassifyWorkspace(workspacePath);
        if (workspace is null)
        {
            return new CSharpWorkspaceSemanticContexts([], sourceDocuments);
        }

        var graphResult = await _projectGraphService.LoadAsync(workspace, cancellationToken).ConfigureAwait(false);
        if (!graphResult.IsSuccess)
        {
            return new CSharpWorkspaceSemanticContexts([], sourceDocuments, graphResult.Error);
        }

        if (graphResult.Value.Projects.Count == 0)
        {
            return new CSharpWorkspaceSemanticContexts([], sourceDocuments);
        }

        var projects = graphResult.Value.Projects;
        foreach (var project in projects)
        {
            await AddEvaluatedSourcesAsync(project, sourceDocuments, openDocuments, cancellationToken, includeGlobalUsings: false)
                .ConfigureAwait(false);
        }
        var referenceResults = await ResolveProjectReferencePathsAsync(projects, cancellationToken).ConfigureAwait(false);
        var projectOwnership = new WorkspaceProjectOwnershipMap(projects);
        var ownedDocuments = sourceDocuments
            .Select(document => new OwnedSourceDocument(
                document,
                projectOwnership.FindOwningProject(Path.GetFullPath(document.Path))))
            .ToArray();
        var projectContexts = new List<CSharpWorkspaceProjectContext>(projects.Count);
        var fallbackDocuments = ownedDocuments
            .Where(static owned => owned.Project is null)
            .Select(static owned => owned.Document)
            .ToList();
        var projectSystemError = OperationError.None;

        foreach (var project in projects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var projectPath = Path.GetFullPath(project.Path);
            var targetDocuments = ownedDocuments
                .Where(owned => owned.Project is not null
                    && PathComparer.Equals(Path.GetFullPath(owned.Project.Path), projectPath)
                    && IsCompiledSource(project, owned.Document.Path))
                .Select(static owned => owned.Document)
                .ToArray();
            if (targetDocuments.Length == 0)
            {
                continue;
            }

            var reachableProjects = _projectCompilationReferenceProvider is null
                ? GetReachableProjectPaths(project, projects)
                : new HashSet<string>([projectPath], PathComparer);
            var metadataReferences = CollectMetadataReferencePaths(reachableProjects, referenceResults);
            if (!metadataReferences.IsSuccess)
            {
                if (projectSystemError.IsNone)
                {
                    projectSystemError = metadataReferences.Error;
                }

                fallbackDocuments.AddRange(targetDocuments);
                continue;
            }

            var contextDocuments = ownedDocuments
                .Where(owned => owned.Project is not null
                    && reachableProjects.Contains(Path.GetFullPath(owned.Project.Path))
                    && IsCompiledSource(owned.Project, owned.Document.Path))
                .Select(static owned => owned.Document)
                .ToList();
            await AddEvaluatedSourcesAsync(project, contextDocuments, openDocuments, cancellationToken).ConfigureAwait(false);
            var semanticContext = CreateProjectContext(targetDocuments[0].Path, contextDocuments, project, metadataReferences.Value);
            projectContexts.Add(new CSharpWorkspaceProjectContext(
                semanticContext,
                targetDocuments.Select(static document => document.Path).ToArray(),
                projectPath));
        }

        var looseDocuments = fallbackDocuments
            .GroupBy(document => Path.GetFullPath(document.Path), PathComparer)
            .Select(static group => group.Last())
            .ToArray();
        return new CSharpWorkspaceSemanticContexts(projectContexts, looseDocuments, projectSystemError);
    }

    private static bool IsCompiledSource(WorkspaceProject project, string path) =>
        project.Metadata.SourcePaths.Count == 0
        || project.Metadata.SourcePaths.Any(source => PathComparer.Equals(Path.GetFullPath(source), Path.GetFullPath(path)));

    private async Task AddEvaluatedSourcesAsync(WorkspaceProject project, List<CSharpSourceDocument> documents,
        IReadOnlyList<CSharpSourceDocument> openDocuments, CancellationToken cancellationToken, bool includeGlobalUsings = true)
    {
        foreach (var path in project.Metadata.SourcePaths.Where(path => Path.GetExtension(path).Equals(".cs", StringComparison.OrdinalIgnoreCase)))
        {
            if (documents.Any(document => PathComparer.Equals(document.Path, path))) continue;
            var open = openDocuments.FirstOrDefault(document => PathComparer.Equals(Path.GetFullPath(document.Path), path));
            if (open is not null)
            {
                documents.Add(open);
            }
            else
            {
                var loaded = await _documentStore.LoadAsync(path, cancellationToken).ConfigureAwait(false);
                if (loaded.IsSuccess) documents.Add(new CSharpSourceDocument(path, loaded.Value.Text));
            }
        }

        if (includeGlobalUsings && project.Metadata.GlobalUsings.Count > 0
            && !documents.Any(document => document.Path.EndsWith(".GlobalUsings.g.cs", StringComparison.OrdinalIgnoreCase)))
        {
            documents.Add(new CSharpSourceDocument(project.Path + ".global-usings.g.cs", string.Join("\n", project.Metadata.GlobalUsings)));
        }
    }

    private static CSharpSemanticContext CreateProjectContext(string activePath, IReadOnlyList<CSharpSourceDocument> documents,
        WorkspaceProject project, IReadOnlyList<string> references) => new(activePath, documents)
    {
        AnalyzerPaths = project.Metadata.AnalyzerPaths,
        AdditionalFilePaths = project.Metadata.AdditionalFilePaths,
        AnalyzerConfigPaths = project.Metadata.AnalyzerConfigPaths,
        MetadataReferencePaths = references,
        DefineConstants = project.Metadata.DefineConstants,
        Nullable = project.Metadata.Nullable,
        LanguageVersion = project.Metadata.LanguageVersion,
        OutputType = project.Metadata.OutputType,
        AllowUnsafe = project.Metadata.AllowUnsafe,
    };

    private async Task<Dictionary<string, Result<IReadOnlyList<string>>>> ResolveProjectReferencePathsAsync(
        IReadOnlyList<WorkspaceProject> projects,
        CancellationToken cancellationToken)
    {
        var results = new Dictionary<string, Result<IReadOnlyList<string>>>(PathComparer);
        if (_projectCompilationReferenceProvider is null)
        {
            return results;
        }

        foreach (var project in projects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var projectPath = Path.GetFullPath(project.Path);
            var targetFramework = project.Metadata.TargetFrameworks.Count > 0
                ? project.Metadata.TargetFrameworks[0]
                : null;
            results[projectPath] = await _projectCompilationReferenceProvider
                .GetReferencePathsAsync(projectPath, targetFramework, cancellationToken)
                .ConfigureAwait(false);
        }

        return results;
    }

    private static Result<string[]> CollectMetadataReferencePaths(
        HashSet<string> reachableProjectPaths,
        Dictionary<string, Result<IReadOnlyList<string>>> referenceResults)
    {
        if (referenceResults.Count == 0)
        {
            return Result.Success(Array.Empty<string>());
        }

        var paths = new List<string>();
        foreach (var projectPath in reachableProjectPaths)
        {
            if (!referenceResults.TryGetValue(projectPath, out var result))
            {
                continue;
            }

            if (!result.IsSuccess)
            {
                return Result.Failure<string[]>(result.Error);
            }

            paths.AddRange(result.Value);
        }

        return Result.Success(paths
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(PathComparer)
            .OrderBy(static path => path, PathComparer)
            .ToArray());
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

    private sealed record OwnedSourceDocument(CSharpSourceDocument Document, WorkspaceProject? Project);
}
