using NUnit.Framework;
using Toren.App.Execution.Models;
using Toren.App.Execution.ViewModels;
using Toren.Core.Results;
using Toren.DotNet.Execution.Contracts;
using Toren.DotNet.Execution.Models;
using Toren.Workspaces.Models;

namespace Toren.UnitTests.Execution;

[TestFixture]
public sealed class WorkspaceExecutionLaunchProfileTests
{
    [Test]
    public async Task RunIncludesSelectedLaunchProfile()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"toren-run-profile-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var projectPath = Path.Combine(directory, "Web.csproj");
            var service = new CapturingCommandService();
            using var viewModel = new WorkspaceExecutionViewModel(service);
            viewModel.SetWorkspace(new WorkspaceDescriptor(directory, "Workspace", WorkspaceKind.Folder));
            viewModel.SetRunTargets(
                [new WorkspaceExecutionTarget(projectPath, "Web", ["net10.0"])]);
            viewModel.SetLaunchProfiles(
                [
                    new DotNetLaunchProfile(
                        "http",
                        false,
                        null,
                        "http://localhost:5000",
                        new Dictionary<string, string>()),
                    new DotNetLaunchProfile(
                        "https",
                        true,
                        "swagger",
                        "https://localhost:7000",
                        new Dictionary<string, string>()),
                ]);
            viewModel.SelectedLaunchProfileIndex = 1;

            await viewModel.ExecuteAsync(DotNetCommandKind.Run);

            Assert.Multiple(() =>
            {
                Assert.That(service.LastRequest, Is.Not.Null);
                Assert.That(service.LastRequest!.LaunchProfile, Is.EqualTo("https"));
                Assert.That(viewModel.SelectedLaunchProfile!.ApplicationUrl, Is.EqualTo("https://localhost:7000"));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void ChangingStartupProjectClearsStaleLaunchProfiles()
    {
        using var viewModel = new WorkspaceExecutionViewModel(new CapturingCommandService());
        viewModel.SetWorkspace(
            new WorkspaceDescriptor(Path.GetTempPath(), "Workspace", WorkspaceKind.Folder));
        viewModel.SetRunTargets(
            [
                new WorkspaceExecutionTarget("First.csproj", "First", ["net10.0"]),
                new WorkspaceExecutionTarget("Second.csproj", "Second", ["net10.0"]),
            ]);
        viewModel.SetLaunchProfiles(
            [new DotNetLaunchProfile("https", true, null, null, new Dictionary<string, string>())]);

        viewModel.SelectedRunTargetIndex = 1;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.LaunchProfiles, Is.Empty);
            Assert.That(viewModel.LaunchProfile, Is.Null);
        });
    }

    private sealed class CapturingCommandService : IDotNetCommandService
    {
        public DotNetCommandRequest? LastRequest { get; private set; }

        public Task<Result<DotNetCommandResult>> ExecuteAsync(
            DotNetCommandRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;
            return Task.FromResult(
                Result.Success(new DotNetCommandResult(request.Kind, 0, string.Empty, string.Empty)));
        }
    }
}
