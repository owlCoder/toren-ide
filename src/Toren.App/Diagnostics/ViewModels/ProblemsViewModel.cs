using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Toren.App.Diagnostics.Models;
using Toren.Language.CSharp.Models;

namespace Toren.App.Diagnostics.ViewModels;

public sealed partial class ProblemsViewModel : ObservableObject
{
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private readonly Dictionary<string, List<ProblemItemViewModel>> _itemsByFile = new(PathComparer);
    private readonly List<ProblemItemViewModel> _workspaceItems = [];
    private readonly Dictionary<string, List<ProblemItemViewModel>> _supplementalItemsBySource =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ProblemItemViewModel> _allItems = [];
    private readonly HashSet<string> _projectFilePaths = new(PathComparer);
    private string? _currentDocumentPath;
    private string? _currentProjectPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFilteredEmpty))]
    private bool _hasItems;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProblems))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(IsFilteredEmpty))]
    private int _count;

    [ObservableProperty]
    private int _errorCount;

    [ObservableProperty]
    private int _warningCount;

    [ObservableProperty]
    private int _infoCount;

    [ObservableProperty]
    private bool _showErrors = true;

    [ObservableProperty]
    private bool _showWarnings = true;

    [ObservableProperty]
    private bool _showInfo = true;

    [ObservableProperty]
    private ProblemsScope _scope = ProblemsScope.Workspace;

    [ObservableProperty]
    private bool _canUseProjectScope;

    [ObservableProperty]
    private bool _canUseCurrentDocumentScope;

    [ObservableProperty]
    private string _projectScopeName = "Project";

    public ObservableCollection<ProblemItemViewModel> Items { get; } = new();

    public bool HasProblems => Count > 0;

    public bool HasAnyProblems => _allItems.Count > 0;

    public bool IsEmpty => Count == 0;

    public bool IsFilteredEmpty => HasProblems && !HasItems;

    public bool IsWorkspaceScope
    {
        get => Scope == ProblemsScope.Workspace;
        set
        {
            if (value)
            {
                Scope = ProblemsScope.Workspace;
            }
        }
    }

    public bool IsProjectScope
    {
        get => Scope == ProblemsScope.Project;
        set
        {
            if (value && CanUseProjectScope)
            {
                Scope = ProblemsScope.Project;
            }
        }
    }

    public bool IsCurrentDocumentScope
    {
        get => Scope == ProblemsScope.CurrentDocument;
        set
        {
            if (value && CanUseCurrentDocumentScope)
            {
                Scope = ProblemsScope.CurrentDocument;
            }
        }
    }

    public void SetScopeContext(ProblemsScopeContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _currentDocumentPath = string.IsNullOrWhiteSpace(context.CurrentDocumentPath)
            ? null
            : Path.GetFullPath(context.CurrentDocumentPath);
        _currentProjectPath = string.IsNullOrWhiteSpace(context.CurrentProjectPath)
            ? null
            : Path.GetFullPath(context.CurrentProjectPath);
        _projectFilePaths.Clear();
        foreach (var path in context.ProjectFilePaths)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                _projectFilePaths.Add(Path.GetFullPath(path));
            }
        }

        CanUseCurrentDocumentScope = _currentDocumentPath is not null;
        CanUseProjectScope = _projectFilePaths.Count > 0 || _currentProjectPath is not null;
        ProjectScopeName = string.IsNullOrWhiteSpace(context.ProjectDisplayName)
            ? "Project"
            : context.ProjectDisplayName;

        if ((Scope == ProblemsScope.Project && !CanUseProjectScope)
            || (Scope == ProblemsScope.CurrentDocument && !CanUseCurrentDocumentScope))
        {
            Scope = ProblemsScope.Workspace;
            return;
        }

        ApplyFilters();
    }

    public void Replace(string filePath, IReadOnlyList<CSharpDiagnostic> diagnostics)
    {
        Clear();
        ReplaceFile(filePath, diagnostics);
    }

    public void ReplaceWorkspace(IReadOnlyList<CSharpDocumentDiagnostics> diagnosticsByFile)
    {
        ArgumentNullException.ThrowIfNull(diagnosticsByFile);
        ReplaceWorkspace(new WorkspaceDiagnosticsSnapshot(diagnosticsByFile, []));
    }

    public void ReplaceWorkspace(WorkspaceDiagnosticsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        _itemsByFile.Clear();
        _workspaceItems.Clear();
        foreach (var documentDiagnostics in snapshot.DocumentDiagnostics)
        {
            var normalizedPath = Path.GetFullPath(documentDiagnostics.FilePath);
            _itemsByFile[normalizedPath] = documentDiagnostics.Diagnostics
                .Select(diagnostic => new ProblemItemViewModel(normalizedPath, diagnostic))
                .ToList();
        }

        _workspaceItems.AddRange(snapshot.WorkspaceDiagnostics.Select(static diagnostic => new ProblemItemViewModel(diagnostic)));
        Rebuild();
    }

    public void ReplaceSupplemental(
        string sourceKey,
        IReadOnlyList<ProblemDiagnostic> diagnostics)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceKey);
        ArgumentNullException.ThrowIfNull(diagnostics);

        if (diagnostics.Count == 0)
        {
            _supplementalItemsBySource.Remove(sourceKey);
        }
        else
        {
            _supplementalItemsBySource[sourceKey] = diagnostics
                .Select(static diagnostic => new ProblemItemViewModel(diagnostic))
                .ToList();
        }

        Rebuild();
    }

    public void ReplaceFile(string filePath, IReadOnlyList<CSharpDiagnostic> diagnostics)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(diagnostics);

        var normalizedPath = Path.GetFullPath(filePath);
        _itemsByFile[normalizedPath] = diagnostics
            .Select(diagnostic => new ProblemItemViewModel(normalizedPath, diagnostic))
            .ToList();
        Rebuild();
    }

    public void RemoveFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (_itemsByFile.Remove(Path.GetFullPath(filePath)))
        {
            Rebuild();
        }
    }

    public void Clear()
    {
        _itemsByFile.Clear();
        _workspaceItems.Clear();
        _supplementalItemsBySource.Clear();
        _allItems.Clear();
        OnPropertyChanged(nameof(HasAnyProblems));
        ApplyFilters();
    }

    partial void OnShowErrorsChanged(bool value)
    {
        ApplyFilters();
    }

    partial void OnShowWarningsChanged(bool value)
    {
        ApplyFilters();
    }

    partial void OnShowInfoChanged(bool value)
    {
        ApplyFilters();
    }

    partial void OnScopeChanged(ProblemsScope value)
    {
        OnPropertyChanged(nameof(IsWorkspaceScope));
        OnPropertyChanged(nameof(IsProjectScope));
        OnPropertyChanged(nameof(IsCurrentDocumentScope));
        ApplyFilters();
    }

    private void Rebuild()
    {
        _allItems.Clear();
        var diagnostics = _workspaceItems
            .Concat(_supplementalItemsBySource.Values.SelectMany(static items => items))
            .Concat(_itemsByFile.Values.SelectMany(static items => items));
        _allItems.AddRange(diagnostics
            .GroupBy(item => string.IsNullOrEmpty(item.FilePath) ? item.ProjectPath ?? string.Empty : item.FilePath, PathComparer)
            .SelectMany(group => group.DistinctBy(item => (
                Source: string.IsNullOrEmpty(item.FilePath) ? item.FileName : string.Empty,
                item.Code, item.Message, item.Severity, item.StartLine, item.StartColumn))));
        _allItems.Sort(CompareItems);
        OnPropertyChanged(nameof(HasAnyProblems));
        ApplyFilters();
    }

    private static int CompareItems(ProblemItemViewModel left, ProblemItemViewModel right)
    {
        var pathComparison = PathComparer.Compare(left.FilePath, right.FilePath);
        if (pathComparison != 0)
        {
            return pathComparison;
        }

        var lineComparison = left.StartLine.CompareTo(right.StartLine);
        if (lineComparison != 0)
        {
            return lineComparison;
        }

        var columnComparison = left.StartColumn.CompareTo(right.StartColumn);
        return columnComparison != 0
            ? columnComparison
            : StringComparer.Ordinal.Compare(left.Code, right.Code);
    }

    private void ApplyFilters()
    {
        var scopedItems = _allItems.Where(IsInSelectedScope).ToArray();
        Count = scopedItems.Length;
        ErrorCount = scopedItems.Count(item => item.IsError);
        WarningCount = scopedItems.Count(item => item.IsWarning);
        InfoCount = scopedItems.Count(item => item.IsInfo);

        Items.Clear();
        foreach (var item in scopedItems)
        {
            if ((item.IsError && ShowErrors)
                || (item.IsWarning && ShowWarnings)
                || (item.IsInfo && ShowInfo))
            {
                Items.Add(item);
            }
        }

        HasItems = Items.Count > 0;
    }

    private bool IsInSelectedScope(ProblemItemViewModel item) =>
        Scope switch
        {
            ProblemsScope.Workspace => true,
            ProblemsScope.Project => IsInSelectedProject(item),
            ProblemsScope.CurrentDocument => _currentDocumentPath is not null
                && !string.IsNullOrEmpty(item.FilePath)
                && PathComparer.Equals(_currentDocumentPath, item.FilePath),
            _ => throw new InvalidOperationException($"Unsupported Problems scope: {Scope}."),
        };

    private bool IsInSelectedProject(ProblemItemViewModel item) =>
        (_currentProjectPath is not null
            && item.ProjectPath is not null
            && PathComparer.Equals(_currentProjectPath, item.ProjectPath))
        || (!string.IsNullOrEmpty(item.FilePath) && _projectFilePaths.Contains(item.FilePath));
}
