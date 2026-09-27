using NUnit.Framework;
using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.DotNet.Environment.Services;

namespace Toren.UnitTests.DotNet;

[TestFixture]
public sealed class DotNetEnvironmentServiceTests
{
    [Test]
    public async Task GetInstalledSdksReturnsParsedSdks()
    {
        var processResult = Result<ProcessResult>.Success(
            new ProcessResult(0, "10.0.100 [/opt/dotnet/sdk]", string.Empty));
        var service = new DotNetEnvironmentService(new StubProcessRunner(processResult));

        var result = await service.GetInstalledSdksAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Has.Count.EqualTo(1));
            Assert.That(result.Value![0].Version, Is.EqualTo("10.0.100"));
        });
    }

    [Test]
    public async Task GetInstalledSdksReturnsFailureWhenProcessCannotStart()
    {
        var processResult = Result<ProcessResult>.Failure(
            Error.Create("process.start.failed", "dotnet was not found"));
        var service = new DotNetEnvironmentService(new StubProcessRunner(processResult));

        var result = await service.GetInstalledSdksAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("dotnet.executable.unavailable"));
        });
    }

    [Test]
    public async Task GetInstalledSdksReturnsFailureForNonZeroExitCode()
    {
        var processResult = Result<ProcessResult>.Success(
            new ProcessResult(1, string.Empty, "SDK discovery failed"));
        var service = new DotNetEnvironmentService(new StubProcessRunner(processResult));

        var result = await service.GetInstalledSdksAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("dotnet.sdk.discovery.failed"));
        });
    }

    private sealed class StubProcessRunner(Result<ProcessResult> result) : IProcessRunner
    {
        public Task<Result<ProcessResult>> RunAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(result);
        }
    }
}
