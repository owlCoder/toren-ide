using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Toren.Language.CSharp.Models;

namespace Toren.App.Diagnostics.ViewModels;

public sealed partial class ProblemsViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _hasItems;

    [ObservableProperty]
    private int _count;

    [ObservableProperty]
    private int _errorCount;

    [ObservableProperty]
    private int _warningCount;

    public ObservableCollection<ProblemItemViewModel> Items { get; } = new();

    public bool IsEmpty => !HasItems;

    public void Replace(string filePath, IReadOnlyList<CSharpDiagnostic> diagnostics)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(diagnostics);

        Items.Clear();
        var errorCount = 0;
        var warningCount = 0;

        foreach (var diagnostic in diagnostics)
        {
            Items.Add(new ProblemItemViewModel(filePath, diagnostic));
            if (diagnostic.Severity == CSharpDiagnosticSeverity.Error)
            {
                errorCount++;
            }
            else if (diagnostic.Severity == CSharpDiagnosticSeverity.Warning)
            {
                warningCount++;
            }
        }

        Count = Items.Count;
        ErrorCount = errorCount;
        WarningCount = warningCount;
        HasItems = Count > 0;
    }

    public void Clear()
    {
        Items.Clear();
        Count = 0;
        ErrorCount = 0;
        WarningCount = 0;
        HasItems = false;
    }
}
