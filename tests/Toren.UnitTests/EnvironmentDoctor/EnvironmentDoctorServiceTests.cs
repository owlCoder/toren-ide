using NUnit.Framework;
using Toren.App.EnvironmentDoctor.Models;
using Toren.App.EnvironmentDoctor.Services;
using Toren.Containers.Contracts;
using Toren.Containers.Models;
using Toren.Core.Results;
using Toren.DotNet.AspNetCore.Contracts;
using Toren.DotNet.AspNetCore.Models;
using Toren.DotNet.Environment.Contracts;
using Toren.Git.Contracts;

namespace Toren.UnitTests.EnvironmentDoctor;

[TestFixture]
public sealed class EnvironmentDoctorServiceTests
{
    private static readonly string[] ExpectedCheckIds =
    [
        "dotnet-sdk",
        "git",
        "docker-compose",
        "https-dev-cert",
    ];

    [Test]
    public async Task CheckReturnsHealthyCoreToolsAndOptionalWarnings()
    {
        var service = new EnvironmentDoctorService(
            new FakeSdkResolver(Result.Success("10.0.100")),
            new FakeGitEnvironmentService(Result.Success("git version 2.51.0")),
            new FakeDockerComposeService(Result.Success(
                new DockerComposeToolStatus(false, null, "Docker is not installed."))),
            new FakeHttpsCertificateService(Result.Success(HttpsDevelopmentCertificateState.ValidUntrusted)));

        var report = await service.CheckAsync(Environment.CurrentDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(report.IsSuccess, Is.True);
            Assert.That(report.Value!.Checks, Has.Count.EqualTo(4));
            Assert.That(report.Value.ErrorCount, Is.Zero);
            Assert.That(report.Value.WarningCount, Is.EqualTo(2));
            Assert.That(report.Value.Checks[0].Status, Is.EqualTo(EnvironmentDoctorStatus.Healthy));
            Assert.That(report.Value.Checks[1].Status, Is.EqualTo(EnvironmentDoctorStatus.Healthy));
            Assert.That(report.Value.Checks[2].Id, Is.EqualTo("docker-compose"));
            Assert.That(report.Value.Checks[2].Status, Is.EqualTo(EnvironmentDoctorStatus.Warning));
            Assert.That(report.Value.Checks[3].Id, Is.EqualTo("https-dev-cert"));
            Assert.That(report.Value.Checks[3].Status, Is.EqualTo(EnvironmentDoctorStatus.Warning));
        });
    }

    [Test]
    public async Task CheckKeepsIndependentFailuresInTheReport()
    {
        var failure = OperationError.Create("tool.unavailable", "Tool unavailable.");
        var service = new EnvironmentDoctorService(
            new FakeSdkResolver(Result.Failure<string>(failure)),
            new FakeGitEnvironmentService(Result.Failure<string>(failure)),
            new FakeDockerComposeService(Result.Failure<DockerComposeToolStatus>(failure)),
            new FakeHttpsCertificateService(Result.Failure<HttpsDevelopmentCertificateState>(failure)));

        var report = await service.CheckAsync(Environment.CurrentDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(report.IsSuccess, Is.True);
            Assert.That(report.Value!.ErrorCount, Is.EqualTo(2));
            Assert.That(report.Value.WarningCount, Is.EqualTo(2));
            Assert.That(report.Value.Checks.Select(check => check.Id), Is.EqualTo(ExpectedCheckIds));
        });
    }

    private sealed class FakeSdkResolver(Result<string> result) : IDotNetSdkResolver
    {
        public Task<Result<string>> ResolveVersionAsync(
            string workingDirectory,
            CancellationToken cancellationToken = default) => Task.FromResult(result);
    }

    private sealed class FakeGitEnvironmentService(Result<string> result) : IGitEnvironmentService
    {
        public Task<Result<string>> GetVersionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }

    private sealed class FakeDockerComposeService(Result<DockerComposeToolStatus> result)
        : IDockerComposeService
    {
        public Task<Result<DockerComposeToolStatus>> DetectAsync(
            CancellationToken cancellationToken = default) => Task.FromResult(result);

        public Task<Result<bool>> UpAsync(
            DockerComposeRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult(Result.Success(true));

        public Task<Result<bool>> DownAsync(
            DockerComposeRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult(Result.Success(true));

        public Task<Result<bool>> BuildAsync(
            DockerComposeRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult(Result.Success(true));

        public Task<Result<string>> GetLogsAsync(
            DockerComposeLogsRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult(Result.Success(string.Empty));
    }

    private sealed class FakeHttpsCertificateService(Result<HttpsDevelopmentCertificateState> result)
        : IHttpsDevelopmentCertificateService
    {
        public Task<Result<HttpsDevelopmentCertificateState>> CheckAsync(
            CancellationToken cancellationToken = default) => Task.FromResult(result);

        public Task<Result<bool>> TrustAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(true));
    }
}
