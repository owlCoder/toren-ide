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
    private readonly List<ProblemItemViewModel> _allItems = [];
    private readonly HashSet<string> _projectFilePaths = new(PathComparer);
    private string? _currentDocumentPath;

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
        _projectFilePaths.Clear();
        foreach (var path in context.ProjectFilePaths)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                _projectFilePaths.Add(Path.GetFullPath(path));
            }
        }

        CanUseCurrentDocumentScope = _currentDocumentPath is not null;
        CanUseProjectScope = _projectFilePaths.Count > 0;
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

        _itemsByFile.Clear();
        foreach (var documentDiagnostics in diagnosticsByFile)
        {
            var normalizedPath = Path.GetFullPath(documentDiagnostics.FilePath);
            _itemsByFile[normalizedPath] = documentDiagnostics.Diagnostics
                .Select(diagnostic => new ProblemItemViewModel(normalizedPath, diagnostic))
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
        _allItems.AddRange(_itemsByFile.Values
            .SelectMany(items => items)
            .OrderBy(item => item.FilePath, PathComparer)
            .ThenBy(item => item.StartLine)
            .ThenBy(item => item.StartColumn)
            .ThenBy(item => item.Code, StringComparer.Ordinal));
        OnPropertyChanged(nameof(HasAnyProblems));
        ApplyFilters();
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
            ProblemsScope.Project => _projectFilePaths.Contains(item.FilePath),
            ProblemsScope.CurrentDocument => _currentDocumentPath is not null
                && PathComparer.Equals(_currentDocumentPath, item.FilePath),
            _ => throw new ArgumentOutOfRangeException(nameof(Scope)),
        };
}
