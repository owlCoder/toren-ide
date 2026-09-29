using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Toren.DotNet.Packages.Contracts;
using Toren.DotNet.Packages.Models;
using Toren.Workspaces.Models;

namespace Toren.App.Packages.ViewModels;

public sealed partial class PackageManagerViewModel(IDotNetPackageService packageService) : ObservableObject
{
    private readonly IDotNetPackageService _packageService = packageService
        ?? throw new ArgumentNullException(nameof(packageService));
    private string? _workingDirectory;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedProject))]
    [NotifyPropertyChangedFor(nameof(CanManagePackages))]
    private int _selectedProjectIndex = -1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedSearchResult))]
    [NotifyPropertyChangedFor(nameof(CanInstallSelected))]
    private int _selectedSearchResultIndex = -1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedInstalledPackage))]
    [NotifyPropertyChangedFor(nameof(CanUpdateSelected))]
    [NotifyPropertyChangedFor(nameof(CanRemoveSelected))]
    private int _selectedInstalledPackageIndex = -1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSearch))]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _customSource = string.Empty;

    [ObservableProperty]
    private bool _includePrerelease;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSearch))]
    [NotifyPropertyChangedFor(nameof(CanManagePackages))]
    [NotifyPropertyChangedFor(nameof(CanInstallSelected))]
    [NotifyPropertyChangedFor(nameof(CanUpdateSelected))]
    [NotifyPropertyChangedFor(nameof(CanRemoveSelected))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "Open a workspace to manage NuGet packages.";

    public ObservableCollection<PackageProjectTarget> Projects { get; } = new();

    public ObservableCollection<NuGetPackageSearchResult> SearchResults { get; } = new();

    public ObservableCollection<NuGetInstalledPackage> InstalledPackages { get; } = new();

    public PackageProjectTarget? SelectedProject =>
        SelectedProjectIndex >= 0 && SelectedProjectIndex < Projects.Count
            ? Projects[SelectedProjectIndex]
            : null;

    public NuGetPackageSearchResult? SelectedSearchResult =>
        SelectedSearchResultIndex >= 0 && SelectedSearchResultIndex < SearchResults.Count
            ? SearchResults[SelectedSearchResultIndex]
            : null;

    public NuGetInstalledPackage? SelectedInstalledPackage =>
        SelectedInstalledPackageIndex >= 0 && SelectedInstalledPackageIndex < InstalledPackages.Count
            ? InstalledPackages[SelectedInstalledPackageIndex]
            : null;

    public bool CanSearch => !IsBusy && _workingDirectory is not null && !string.IsNullOrWhiteSpace(SearchQuery);

    public bool CanManagePackages => !IsBusy && SelectedProject is not null;

    public bool CanInstallSelected => !IsBusy && SelectedProject is not null && SelectedSearchResult is not null;

    public bool CanUpdateSelected => !IsBusy && SelectedProject is not null && SelectedInstalledPackage is not null;

    public bool CanRemoveSelected => CanUpdateSelected;

    public void SetWorkspace(string? workingDirectory, IReadOnlyList<WorkspaceProject> projects)
    {
        ArgumentNullException.ThrowIfNull(projects);
        _workingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
            ? null
            : Path.GetFullPath(workingDirectory);

        Projects.Clear();
        foreach (var project in projects.OrderBy(static project => project.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            Projects.Add(new PackageProjectTarget(Path.GetFullPath(project.Path), project.DisplayName));
        }

        SelectedProjectIndex = Projects.Count > 0 ? 0 : -1;
        SearchResults.Clear();
        InstalledPackages.Clear();
        SelectedSearchResultIndex = -1;
        SelectedInstalledPackageIndex = -1;
        StatusText = _workingDirectory is null
            ? "Open a workspace to manage NuGet packages."
            : Projects.Count == 0
                ? "No .NET projects were found in this workspace."
                : "Select a project, then search or inspect installed packages.";
        NotifyAvailability();
    }

    public async Task SearchAsync(CancellationToken cancellationToken = default)
    {
        if (!CanSearch || _workingDirectory is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var sources = string.IsNullOrWhiteSpace(CustomSource)
                ? null
                : new[] { CustomSource.Trim() };
            var result = await _packageService
                .SearchAsync(
                    _workingDirectory,
                    SearchQuery.Trim(),
                    sources,
                    includePrerelease: IncludePrerelease,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(true);

            SearchResults.Clear();
            SelectedSearchResultIndex = -1;
            if (result.IsFailure)
            {
                StatusText = result.Error.Message;
                return;
            }

            foreach (var package in result.Value!)
            {
                SearchResults.Add(package);
            }

            SelectedSearchResultIndex = SearchResults.Count > 0 ? 0 : -1;
            StatusText = SearchResults.Count == 0
                ? "No matching packages found."
                : $"{SearchResults.Count} package{(SearchResults.Count == 1 ? string.Empty : "s")} found.";
        }
        finally
        {
            IsBusy = false;
            NotifyAvailability();
        }
    }

    public async Task RefreshInstalledAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedProject is not { } project || IsBusy)
        {
            InstalledPackages.Clear();
            SelectedInstalledPackageIndex = -1;
            NotifyAvailability();
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _packageService
                .GetInstalledAsync(project.Path, cancellationToken)
                .ConfigureAwait(true);
            InstalledPackages.Clear();
            SelectedInstalledPackageIndex = -1;
            if (result.IsFailure)
            {
                StatusText = result.Error.Message;
                return;
            }

            foreach (var package in result.Value!)
            {
                InstalledPackages.Add(package);
            }

            SelectedInstalledPackageIndex = InstalledPackages.Count > 0 ? 0 : -1;
            StatusText = InstalledPackages.Count == 0
                ? $"{project.DisplayName} has no top-level package references."
                : $"{InstalledPackages.Count} installed package reference{(InstalledPackages.Count == 1 ? string.Empty : "s")} in {project.DisplayName}.";
        }
        finally
        {
            IsBusy = false;
            NotifyAvailability();
        }
    }

    public Task InstallSelectedAsync(CancellationToken cancellationToken = default)
    {
        if (!CanInstallSelected || SelectedProject is not { } project || SelectedSearchResult is not { } package)
        {
            return Task.CompletedTask;
        }

        return RunMutationAsync(
            token => _packageService.AddAsync(
                project.Path,
                package.Id,
                package.LatestVersion,
                package.SourceName,
                token),
            $"Installed {package.Id} {package.LatestVersion}.",
            cancellationToken);
    }

    public Task UpdateSelectedAsync(CancellationToken cancellationToken = default)
    {
        if (!CanUpdateSelected || SelectedProject is not { } project || SelectedInstalledPackage is not { } package)
        {
            return Task.CompletedTask;
        }

        return RunMutationAsync(
            token => _packageService.UpdateAsync(project.Path, package.Id, cancellationToken: token),
            $"Updated {package.Id}.",
            cancellationToken);
    }

    public Task RemoveSelectedAsync(CancellationToken cancellationToken = default)
    {
        if (!CanRemoveSelected || SelectedProject is not { } project || SelectedInstalledPackage is not { } package)
        {
            return Task.CompletedTask;
        }

        return RunMutationAsync(
            token => _packageService.RemoveAsync(project.Path, package.Id, token),
            $"Removed {package.Id}.",
            cancellationToken);
    }

    private async Task RunMutationAsync(
        Func<CancellationToken, Task<Toren.Core.Results.Result<bool>>> action,
        string successMessage,
        CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            var result = await action(cancellationToken).ConfigureAwait(true);
            if (result.IsFailure)
            {
                StatusText = result.Error.Message;
                return;
            }

            await RefreshInstalledCoreAsync(cancellationToken).ConfigureAwait(true);
            StatusText = successMessage;
        }
        finally
        {
            IsBusy = false;
            NotifyAvailability();
        }
    }

    private async Task RefreshInstalledCoreAsync(CancellationToken cancellationToken)
    {
        if (SelectedProject is not { } project)
        {
            return;
        }

        var result = await _packageService.GetInstalledAsync(project.Path, cancellationToken).ConfigureAwait(true);
        InstalledPackages.Clear();
        SelectedInstalledPackageIndex = -1;
        if (result.IsFailure)
        {
            StatusText = result.Error.Message;
            return;
        }

        foreach (var package in result.Value!)
        {
            InstalledPackages.Add(package);
        }

        SelectedInstalledPackageIndex = InstalledPackages.Count > 0 ? 0 : -1;
    }

    private void NotifyAvailability()
    {
        OnPropertyChanged(nameof(SelectedProject));
        OnPropertyChanged(nameof(SelectedSearchResult));
        OnPropertyChanged(nameof(SelectedInstalledPackage));
        OnPropertyChanged(nameof(CanSearch));
        OnPropertyChanged(nameof(CanManagePackages));
        OnPropertyChanged(nameof(CanInstallSelected));
        OnPropertyChanged(nameof(CanUpdateSelected));
        OnPropertyChanged(nameof(CanRemoveSelected));
    }
}
