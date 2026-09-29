using NUnit.Framework;
using Toren.App.Packages.ViewModels;
using Toren.Core.Results;
using Toren.DotNet.Packages.Contracts;
using Toren.DotNet.Packages.Models;
using Toren.Workspaces.Models;

namespace Toren.UnitTests.Packages;

[TestFixture]
public sealed class PackageManagerViewModelTests
{
    [Test]
    public void SetWorkspaceMapsAndSortsAllProjects()
    {
        var service = new StubPackageService();
        var viewModel = new PackageManagerViewModel(service);
        var root = Path.GetTempPath();

        viewModel.SetWorkspace(
            root,
            [CreateProject(root, "Zeta"), CreateProject(root, "Alpha")]);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Projects, Has.Count.EqualTo(2));
            Assert.That(viewModel.Projects[0].DisplayName, Is.EqualTo("Alpha"));
            Assert.That(viewModel.Projects[1].DisplayName, Is.EqualTo("Zeta"));
            Assert.That(viewModel.SelectedProjectIndex, Is.EqualTo(0));
            Assert.That(viewModel.CanManagePackages, Is.True);
        });
    }

    [Test]
    public async Task SearchUsesCustomSourceAndPrereleasePreference()
    {
        var service = new StubPackageService
        {
            SearchPackages =
            [
                new NuGetPackageSearchResult(
                    "Example.Package",
                    "2.0.0-preview.1",
                    100,
                    "Example",
                    "https://feed.example/v3/index.json"),
            ],
        };
        var root = Path.GetTempPath();
        var viewModel = new PackageManagerViewModel(service);
        viewModel.SetWorkspace(root, [CreateProject(root, "App")]);
        viewModel.SearchQuery = "Example";
        viewModel.CustomSource = "https://feed.example/v3/index.json";
        viewModel.IncludePrerelease = true;

        await viewModel.SearchAsync();

        Assert.Multiple(() =>
        {
            Assert.That(service.SearchRequests, Has.Count.EqualTo(1));
            Assert.That(service.SearchRequests[0].Query, Is.EqualTo("Example"));
            Assert.That(service.SearchRequests[0].Source, Is.EqualTo("https://feed.example/v3/index.json"));
            Assert.That(service.SearchRequests[0].IncludePrerelease, Is.True);
            Assert.That(viewModel.SearchResults, Has.Count.EqualTo(1));
            Assert.That(viewModel.SelectedSearchResultIndex, Is.EqualTo(0));
            Assert.That(viewModel.StatusText, Is.EqualTo("1 package found."));
        });
    }

    [Test]
    public async Task InstallSelectedUsesSearchVersionAndSourceThenRefreshesInstalled()
    {
        var root = Path.GetTempPath();
        var project = CreateProject(root, "App");
        var service = new StubPackageService
        {
            SearchPackages =
            [
                new NuGetPackageSearchResult(
                    "Example.Package",
                    "2.0.0",
                    null,
                    null,
                    "https://api.nuget.org/v3/index.json"),
            ],
            InstalledPackages =
            [
                new NuGetInstalledPackage(project.Path, "net10.0", "Example.Package", "2.0.0", "2.0.0"),
            ],
        };
        var viewModel = new PackageManagerViewModel(service);
        viewModel.SetWorkspace(root, [project]);
        viewModel.SearchQuery = "Example";
        await viewModel.SearchAsync();

        await viewModel.InstallSelectedAsync();

        Assert.Multiple(() =>
        {
            Assert.That(service.AddRequests, Has.Count.EqualTo(1));
            Assert.That(service.AddRequests[0].PackageId, Is.EqualTo("Example.Package"));
            Assert.That(service.AddRequests[0].Version, Is.EqualTo("2.0.0"));
            Assert.That(service.AddRequests[0].Source, Is.EqualTo("https://api.nuget.org/v3/index.json"));
            Assert.That(service.InstalledRequestCount, Is.EqualTo(1));
            Assert.That(viewModel.InstalledPackages, Has.Count.EqualTo(1));
            Assert.That(viewModel.StatusText, Is.EqualTo("Installed Example.Package 2.0.0."));
        });
    }

    [Test]
    public async Task UpdateAndRemoveSelectedRefreshInstalledPackages()
    {
        var root = Path.GetTempPath();
        var project = CreateProject(root, "App");
        var service = new StubPackageService
        {
            InstalledPackages =
            [
                new NuGetInstalledPackage(project.Path, "net10.0", "Example.Package", "1.0.0", "1.0.0"),
            ],
        };
        var viewModel = new PackageManagerViewModel(service);
        viewModel.SetWorkspace(root, [project]);
        await viewModel.RefreshInstalledAsync();

        await viewModel.UpdateSelectedAsync();
        await viewModel.RemoveSelectedAsync();

        Assert.Multiple(() =>
        {
            Assert.That(service.UpdateRequests, Has.Count.EqualTo(1));
            Assert.That(service.UpdateRequests[0], Is.EqualTo("Example.Package"));
            Assert.That(service.RemoveRequests, Has.Count.EqualTo(1));
            Assert.That(service.RemoveRequests[0], Is.EqualTo("Example.Package"));
            Assert.That(service.InstalledRequestCount, Is.EqualTo(3));
        });
    }

    private static WorkspaceProject CreateProject(string root, string name) =>
        new(
            Path.Combine(root, $"{name}.csproj"),
            name,
            new ProjectMetadata(
                ["net10.0"],
                "Library",
                name,
                name,
                false,
                false,
                null,
                null,
                null),
            []);

    private sealed class StubPackageService : IDotNetPackageService
    {
        public IReadOnlyList<NuGetPackageSearchResult> SearchPackages { get; init; } = [];

        public IReadOnlyList<NuGetInstalledPackage> InstalledPackages { get; init; } = [];

        public List<(string Query, string? Source, bool IncludePrerelease)> SearchRequests { get; } = [];

        public List<(string PackageId, string? Version, string? Source)> AddRequests { get; } = [];

        public List<string> UpdateRequests { get; } = [];

        public List<string> RemoveRequests { get; } = [];

        public int InstalledRequestCount { get; private set; }

        public Task<Result<IReadOnlyList<NuGetPackageSearchResult>>> SearchAsync(
            string workingDirectory,
            string query,
            IReadOnlyList<string>? sources = null,
            int skip = 0,
            int take = 20,
            bool includePrerelease = false,
            CancellationToken cancellationToken = default)
        {
            SearchRequests.Add((query, sources?.SingleOrDefault(), includePrerelease));
            return Task.FromResult(Result.Success(SearchPackages));
        }

        public Task<Result<IReadOnlyList<NuGetInstalledPackage>>> GetInstalledAsync(
            string projectPath,
            CancellationToken cancellationToken = default)
        {
            InstalledRequestCount++;
            return Task.FromResult(Result.Success(InstalledPackages));
        }

        public Task<Result<bool>> AddAsync(
            string projectPath,
            string packageId,
            string? version = null,
            string? source = null,
            CancellationToken cancellationToken = default)
        {
            AddRequests.Add((packageId, version, source));
            return Task.FromResult(Result.Success(true));
        }

        public Task<Result<bool>> UpdateAsync(
            string projectPath,
            string packageId,
            string? version = null,
            CancellationToken cancellationToken = default)
        {
            UpdateRequests.Add(packageId);
            return Task.FromResult(Result.Success(true));
        }

        public Task<Result<bool>> RemoveAsync(
            string projectPath,
            string packageId,
            CancellationToken cancellationToken = default)
        {
            RemoveRequests.Add(packageId);
            return Task.FromResult(Result.Success(true));
        }
    }
}
