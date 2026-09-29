using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Toren.App.Testing.Models;

namespace Toren.App.Testing.ViewModels;

public sealed partial class TestExplorerViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusText = "Open a workspace to discover tests.";

    public ObservableCollection<WorkspaceTestProjectDiscovery> Projects { get; } = new();

    public bool HasProjects => Projects.Count > 0;

    public bool IsEmpty => !IsLoading && !HasProjects;

    public int TotalTests => Projects.Sum(static project => project.Tests.Count);

    public void BeginRefresh()
    {
        IsLoading = true;
        StatusText = "Discovering tests…";
    }

    public void Replace(IReadOnlyList<WorkspaceTestProjectDiscovery> projects)
    {
        ArgumentNullException.ThrowIfNull(projects);
        Projects.Clear();
        foreach (var project in projects)
        {
            Projects.Add(project);
        }

        IsLoading = false;
        StatusText = Projects.Count == 0
            ? "No test projects found."
            : $"Discovered {TotalTests} test{(TotalTests == 1 ? string.Empty : "s")} in {Projects.Count} project{(Projects.Count == 1 ? string.Empty : "s")}.";
        NotifyCollectionSummary();
    }

    public void SetError(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Projects.Clear();
        IsLoading = false;
        StatusText = message;
        NotifyCollectionSummary();
    }

    public void Reset()
    {
        Projects.Clear();
        IsLoading = false;
        StatusText = "Open a workspace to discover tests.";
        NotifyCollectionSummary();
    }

    private void NotifyCollectionSummary()
    {
        OnPropertyChanged(nameof(HasProjects));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(TotalTests));
    }
}
