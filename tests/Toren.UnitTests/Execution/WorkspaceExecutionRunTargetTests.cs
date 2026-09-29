using NUnit.Framework;
using Toren.App.Execution.Models;
using Toren.App.Execution.ViewModels;
using Toren.Core.Results;
using Toren.DotNet.Execution.Contracts;
using Toren.DotNet.Execution.Models;
using Toren.Workspaces.Models;

namespace Toren.UnitTests.Execution;

[TestFixture]
public sealed class WorkspaceExecutionRunTargetTests
{
    [Test]
    public async Task RunUsesSelectedProjectConfigurationAndTargetFramework()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"toren-run-target-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var projectPath = Path.Combine(directory, "App.csproj");
            var service = new CapturingCommandService();
            using var viewModel = new WorkspaceExecutionViewModel(service)
            {
                ConfigurationIndex = 1,
            };
            viewModel.SetWorkspace(new WorkspaceDescriptor(directory, "Workspace", WorkspaceKind.Folder));
            viewModel.SetRunTargets(
                [new WorkspaceExecutionTarget(projectPath, "App", ["net8.0", "net10.0"])]);
            viewModel.SelectedTargetFrameworkIndex = 1;

            await viewModel.ExecuteAsync(DotNetCommandKind.Run);

            Assert.That(service.LastRequest, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(service.LastRequest!.Kind, Is.EqualTo(DotNetCommandKind.Run));
                Assert.That(service.LastRequest.WorkingDirectory, Is.EqualTo(directory));
                Assert.That(service.LastRequest.TargetPath, Is.EqualTo(Path.GetFullPath(projectPath)));
                Assert.That(service.LastRequest.Configuration, Is.EqualTo("Release"));
                Assert.That(service.LastRequest.TargetFramework, Is.EqualTo("net10.0"));
                Assert.That(viewModel.CanRun, Is.True);
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void LibraryOnlyWorkspaceKeepsRunDisabled()
    {
        using var viewModel = new WorkspaceExecutionViewModel(new CapturingCommandService());
        viewModel.SetWorkspace(
            new WorkspaceDescriptor(Path.GetTempPath(), "Workspace", WorkspaceKind.Folder));
        viewModel.SetRunTargets([]);

        Assert.That(viewModel.CanRun, Is.False);
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
                Result.Success(
                    new DotNetCommandResult(request.Kind, 0, "run output", string.Empty)));
        }
    }
}
