using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Toren.Language.CSharp.Models;

namespace Toren.App.Diagnostics.ViewModels;

public sealed partial class ProblemsViewModel : ObservableObject
{
    private readonly List<ProblemItemViewModel> _allItems = [];

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

    public ObservableCollection<ProblemItemViewModel> Items { get; } = new();

    public bool HasProblems => Count > 0;

    public bool IsEmpty => Count == 0;

    public bool IsFilteredEmpty => HasProblems && !HasItems;

    public void Replace(string filePath, IReadOnlyList<CSharpDiagnostic> diagnostics)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(diagnostics);

        _allItems.Clear();
        var errorCount = 0;
        var warningCount = 0;
        var infoCount = 0;

        foreach (var diagnostic in diagnostics)
        {
            _allItems.Add(new ProblemItemViewModel(filePath, diagnostic));
            switch (diagnostic.Severity)
            {
                case CSharpDiagnosticSeverity.Error:
                    errorCount++;
                    break;
                case CSharpDiagnosticSeverity.Warning:
                    warningCount++;
                    break;
                case CSharpDiagnosticSeverity.Info:
                    infoCount++;
                    break;
            }
        }

        Count = _allItems.Count;
        ErrorCount = errorCount;
        WarningCount = warningCount;
        InfoCount = infoCount;
        ApplyFilters();
    }

    public void Clear()
    {
        _allItems.Clear();
        Items.Clear();
        Count = 0;
        ErrorCount = 0;
        WarningCount = 0;
        InfoCount = 0;
        HasItems = false;
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

    private void ApplyFilters()
    {
        Items.Clear();
        foreach (var item in _allItems)
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
}
