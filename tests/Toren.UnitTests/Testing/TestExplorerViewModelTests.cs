using NUnit.Framework;
using Toren.App.Testing.Models;
using Toren.App.Testing.ViewModels;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.DotNet.Testing.Contracts;
using Toren.DotNet.Testing.Models;

namespace Toren.UnitTests.Testing;

[TestFixture]
public sealed class TestExplorerViewModelTests
{
    [Test]
    public async Task RerunFailedProjectsOnlyRunsProjectsThatFailedLastRunAll()
    {
        var projectA = new WorkspaceTestProjectDiscovery(
            Path.GetFullPath(Path.Combine("repo", "ATests.csproj")),
            "A.Tests",
            [new DotNetTestCase("A.Tests.Sample.Passes", "Passes")]);
        var projectB = new WorkspaceTestProjectDiscovery(
            Path.GetFullPath(Path.Combine("repo", "BTests.csproj")),
            "B.Tests",
            [new DotNetTestCase("B.Tests.Sample.Passes", "Passes")]);
        var service = new SequencedTestRunService(1, 0, 0);
        using var viewModel = new TestExplorerViewModel(service);
        viewModel.Replace([projectA, projectB]);

        await viewModel.RunAllAsync();

        Assert.Multiple(() =>
        {
            Assert.That(service.ProjectPaths, Is.EqualTo(new[] { projectA.ProjectPath, projectB.ProjectPath }));
            Assert.That(viewModel.HasFailedProjects, Is.True);
            Assert.That(viewModel.CanRerunFailedProjects, Is.True);
        });

        await viewModel.RerunFailedProjectsAsync();

        Assert.Multiple(() =>
        {
            Assert.That(
                service.ProjectPaths,
                Is.EqualTo(new[] { projectA.ProjectPath, projectB.ProjectPath, projectA.ProjectPath }));
            Assert.That(viewModel.HasFailedProjects, Is.False);
            Assert.That(viewModel.CanRerunFailedProjects, Is.False);
            Assert.That(viewModel.StatusText, Is.EqualTo("All previously failed test projects passed."));
        });
    }

    private sealed class SequencedTestRunService(params int[] exitCodes) : IDotNetTestRunService
    {
        private readonly Queue<int> _exitCodes = new(exitCodes);

        public List<string> ProjectPaths { get; } = [];

        public Task<Result<ProcessResult>> RunAsync(
            DotNetTestRunRequest request,
            Action<ProcessOutputLine> onOutput,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ProjectPaths.Add(request.ProjectPath);
            var exitCode = _exitCodes.Dequeue();
            return Task.FromResult(Result.Success(new ProcessResult(exitCode, string.Empty, string.Empty)));
        }
    }
}
