using NUnit.Framework;
using Toren.App.Debugging.Services;
using Toren.App.Debugging.ViewModels;
using Toren.Core.Results;
using Toren.Debugging.Contracts;

namespace Toren.UnitTests.Debugging;

[TestFixture]
public sealed class DebugSessionBreakpointToggleTests
{
    [Test]
    public async Task ToggleBreakpointAddsThenRemovesPendingBreakpoint()
    {
        await using var coordinator = new DebugSessionCoordinator(new UnusedSessionService());
        var viewModel = new DebugSessionViewModel(coordinator);
        var sourcePath = Path.Combine(Path.GetTempPath(), "toren-breakpoint-toggle", "Program.cs");

        var added = await viewModel.ToggleBreakpointAsync(sourcePath, 12, " count > 3 ");

        Assert.Multiple(() =>
        {
            Assert.That(added, Is.True);
            Assert.That(viewModel.Breakpoints, Has.Count.EqualTo(1));
            Assert.That(viewModel.Breakpoints[0].Line, Is.EqualTo(12));
            Assert.That(viewModel.Breakpoints[0].Condition, Is.EqualTo("count > 3"));
            Assert.That(viewModel.Breakpoints[0].IsVerified, Is.False);
        });

        var removed = await viewModel.ToggleBreakpointAsync(sourcePath, 12);

        Assert.Multiple(() =>
        {
            Assert.That(removed, Is.False);
            Assert.That(viewModel.Breakpoints, Is.Empty);
            Assert.That(viewModel.HasBreakpoints, Is.False);
        });
    }

    private sealed class UnusedSessionService : IDebugSessionService
    {
        public Task<Result<IDebugSession>> AttachAsync(
            int processId,
            string? workingDirectory = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The breakpoint toggle test must not attach a debug session.");
    }
}
