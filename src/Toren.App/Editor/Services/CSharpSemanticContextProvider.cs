using Toren.App.Documents.Contracts;
using Toren.App.Editor.Contracts;
using Toren.App.Editor.Models;
using Toren.Core.IO;
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
    ITextDocumentStore documentStore) : ICSharpSemanticContextProvider
{
    private static readonly StringComparer PathComparer = FileSystemPath.Comparer;

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

        var workspace = _workspaceClassifier.ClassifyPath(workspacePath);
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

        var reachableProjects = GetContextProjectPaths(activeProject, projects);
        var metadataReferences = ResolveMetadataReferences(activeProject);
        if (!metadataReferences.IsSuccess)
        {
            return null;
        }

        var openDocumentsByPath = IndexOpenDocuments(openDocuments);
        var sourceDocuments = await LoadSourceDocumentsAsync(
            workspacePath,
            openDocumentsByPath,
            fullPath =>
            {
                var owner = projectOwnership.FindOwningProject(fullPath);
                return owner is not null && reachableProjects.Contains(Path.GetFullPath(owner.Path))
                    && IsCompiledSource(projectOwnership, owner, fullPath);
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

        await AddEvaluatedSourcesAsync(
            activeProject,
            sourceDocuments,
            IndexPaths(sourceDocuments),
            openDocumentsByPath,
            loadedDocuments: null,
            cancellationToken).ConfigureAwait(false);
        AddGlobalUsings(activeProject, sourceDocuments);
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
            IndexOpenDocuments(openDocuments),
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

        var openDocumentsByPath = IndexOpenDocuments(openDocuments);
        var sourceDocuments = await LoadSourceDocumentsAsync(
            workspacePath,
            openDocumentsByPath,
            static _ => true,
            cancellationToken).ConfigureAwait(false);
        if (sourceDocuments is null)
        {
            return null;
        }

        var workspace = _workspaceClassifier.ClassifyPath(workspacePath);
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
        var sourcePaths = IndexPaths(sourceDocuments);
        foreach (var project in projects)
        {
            await AddEvaluatedSourcesAsync(
                project,
                sourceDocuments,
                sourcePaths,
                openDocumentsByPath,
                loadedDocuments: null,
                cancellationToken).ConfigureAwait(false);
        }

        // Classify every document once; the per-project passes below then only touch their own files.
        var projectOwnership = new WorkspaceProjectOwnershipMap(projects);
        var loadedDocuments = new Dictionary<string, CSharpSourceDocument>(sourceDocuments.Count, PathComparer);
        var compiledDocuments = new Dictionary<WorkspaceProject, List<CSharpSourceDocument>>(
            ReferenceEqualityComparer.Instance);
        var fallbackDocuments = new List<CSharpSourceDocument>();
        foreach (var document in sourceDocuments)
        {
            loadedDocuments.TryAdd(document.Path, document);
            var owner = projectOwnership.FindOwningProject(Path.GetFullPath(document.Path));
            if (owner is null)
            {
                fallbackDocuments.Add(document);
            }
            else if (IsCompiledSource(projectOwnership, owner, document.Path))
            {
                if (!compiledDocuments.TryGetValue(owner, out var ownerDocuments))
                {
                    compiledDocuments.Add(owner, ownerDocuments = []);
                }

                ownerDocuments.Add(document);
            }
        }

        var projectContexts = new List<CSharpWorkspaceProjectContext>(projects.Count);
        var projectSystemError = OperationError.None;

        foreach (var project in projects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!compiledDocuments.TryGetValue(project, out var targetDocuments))
            {
                continue;
            }

            var metadataReferences = ResolveMetadataReferences(project);
            if (!metadataReferences.IsSuccess)
            {
                if (projectSystemError.IsNone)
                {
                    projectSystemError = metadataReferences.Error;
                }

                fallbackDocuments.AddRange(targetDocuments);
                continue;
            }

            List<CSharpSourceDocument> contextDocuments;
            if (UsesEvaluatedReferences(project))
            {
                contextDocuments = new List<CSharpSourceDocument>(targetDocuments);
            }
            else
            {
                // Without reference assemblies, referenced projects take part as source.
                var reachableProjects = GetReachableProjectPaths(project, projects);
                contextDocuments = sourceDocuments
                    .Where(document => projectOwnership.FindOwningProject(Path.GetFullPath(document.Path)) is { } owner
                        && reachableProjects.Contains(Path.GetFullPath(owner.Path))
                        && IsCompiledSource(projectOwnership, owner, document.Path))
                    .ToList();
            }

            await AddEvaluatedSourcesAsync(
                project,
                contextDocuments,
                IndexPaths(contextDocuments),
                openDocumentsByPath,
                loadedDocuments,
                cancellationToken).ConfigureAwait(false);
            AddGlobalUsings(project, contextDocuments);
            var semanticContext = CreateProjectContext(targetDocuments[0].Path, contextDocuments, project, metadataReferences.Value);
            projectContexts.Add(new CSharpWorkspaceProjectContext(
                semanticContext,
                targetDocuments.Select(static document => document.Path).ToArray(),
                Path.GetFullPath(project.Path)));
        }

        var looseDocuments = fallbackDocuments
            .GroupBy(document => Path.GetFullPath(document.Path), PathComparer)
            .Select(static group => group.Last())
            .ToArray();
        return new CSharpWorkspaceSemanticContexts(projectContexts, looseDocuments, projectSystemError);
    }

    private static bool IsCompiledSource(
        WorkspaceProjectOwnershipMap projectOwnership,
        WorkspaceProject project,
        string path) =>
        project.Metadata.SourcePaths.Count == 0 || projectOwnership.ListsSource(project, path);

    private static Dictionary<string, CSharpSourceDocument> IndexOpenDocuments(
        IReadOnlyList<CSharpSourceDocument> openDocuments)
    {
        var openDocumentsByPath = new Dictionary<string, CSharpSourceDocument>(openDocuments.Count, PathComparer);
        foreach (var document in openDocuments)
        {
            openDocumentsByPath.TryAdd(Path.GetFullPath(document.Path), document);
        }

        return openDocumentsByPath;
    }

    private static HashSet<string> IndexPaths(List<CSharpSourceDocument> documents)
    {
        var paths = new HashSet<string>(documents.Count, PathComparer);
        foreach (var document in documents)
        {
            paths.Add(document.Path);
        }

        return paths;
    }

    /// <summary>
    /// Adds the project's evaluated compile items that the workspace listing did not provide,
    /// such as generated or linked files, preferring open editor text over the file on disk.
    /// </summary>
    private async Task AddEvaluatedSourcesAsync(
        WorkspaceProject project,
        List<CSharpSourceDocument> documents,
        HashSet<string> documentPaths,
        Dictionary<string, CSharpSourceDocument> openDocumentsByPath,
        Dictionary<string, CSharpSourceDocument>? loadedDocuments,
        CancellationToken cancellationToken)
    {
        foreach (var path in project.Metadata.SourcePaths)
        {
            if (!Path.GetExtension(path.AsSpan()).Equals(".cs", StringComparison.OrdinalIgnoreCase)
                || !documentPaths.Add(path))
            {
                continue;
            }

            if (openDocumentsByPath.TryGetValue(path, out var open))
            {
                documents.Add(open);
            }
            else if (loadedDocuments is not null && loadedDocuments.TryGetValue(path, out var loadedDocument))
            {
                documents.Add(loadedDocument);
            }
            else
            {
                var loaded = await _documentStore.LoadAsync(path, cancellationToken).ConfigureAwait(false);
                if (loaded.IsSuccess)
                {
                    documents.Add(new CSharpSourceDocument(path, loaded.Value.Text));
                }
            }
        }
    }

    private static void AddGlobalUsings(WorkspaceProject project, List<CSharpSourceDocument> documents)
    {
        if (project.Metadata.GlobalUsings.Count > 0
            && !documents.Any(document => document.Path.EndsWith(".GlobalUsings.g.cs", StringComparison.OrdinalIgnoreCase)))
        {
            documents.Add(new CSharpSourceDocument(project.Path + ".global-usings.g.cs", string.Join("\n", project.Metadata.GlobalUsings)));
        }
    }

    private static CSharpSemanticContext CreateProjectContext(string activePath, IReadOnlyList<CSharpSourceDocument> documents,
        WorkspaceProject project, IReadOnlyList<string> references) => new(activePath, documents)
    {
        ProjectPath = project.Path,
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

    /// <summary>
    /// Projects evaluated by the design-time targets compile against their resolved reference
    /// assemblies. Projects without that information fall back to referenced project sources.
    /// </summary>
    private static bool UsesEvaluatedReferences(WorkspaceProject project) =>
        project.Metadata.ReferencePaths is not null || !project.Metadata.CompilerInputsError.IsNone;

    private static Result<IReadOnlyList<string>> ResolveMetadataReferences(WorkspaceProject project) =>
        UsesEvaluatedReferences(project)
            ? ProjectCompilationReferenceResolver.Resolve(project.Metadata)
            : Result.Success<IReadOnlyList<string>>([]);

    private static HashSet<string> GetContextProjectPaths(
        WorkspaceProject project,
        IReadOnlyList<WorkspaceProject> projects) =>
        UsesEvaluatedReferences(project)
            ? new HashSet<string>([Path.GetFullPath(project.Path)], PathComparer)
            : GetReachableProjectPaths(project, projects);

    private async Task<List<CSharpSourceDocument>?> LoadSourceDocumentsAsync(
        string workspacePath,
        Dictionary<string, CSharpSourceDocument> openDocumentsByPath,
        Func<string, bool> includeFile,
        CancellationToken cancellationToken)
    {
        var filesResult = await _workspaceFileProvider.GetFilesAsync(workspacePath, cancellationToken).ConfigureAwait(false);
        if (!filesResult.IsSuccess)
        {
            return null;
        }

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

            if (openDocumentsByPath.TryGetValue(fullPath, out var open))
            {
                sourceDocuments.Add(new CSharpSourceDocument(fullPath, open.Text));
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
}
