using NUnit.Framework;
using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.DotNet.AspNetCore.Models;
using Toren.DotNet.AspNetCore.Services;

namespace Toren.UnitTests.DotNet;

[TestFixture]
public sealed class HttpsDevelopmentCertificateServiceTests
{
    [Test]
    public async Task TrustedCertificateUsesSingleTrustedCheck()
    {
        var runner = new SequenceProcessRunner(
            Result.Success(new ProcessResult(0, string.Empty, string.Empty)));
        var service = new HttpsDevelopmentCertificateService(runner);

        var result = await service.CheckAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(HttpsDevelopmentCertificateState.Trusted));
            Assert.That(runner.Requests, Has.Count.EqualTo(1));
            Assert.That(
                string.Join('\u001F', runner.Requests[0].Arguments),
                Is.EqualTo("dev-certs\u001Fhttps\u001F--check\u001F--trust"));
        });
    }

    [Test]
    public async Task ValidButUntrustedCertificateFallsBackToValidityCheck()
    {
        var runner = new SequenceProcessRunner(
            Result.Success(new ProcessResult(1, string.Empty, string.Empty)),
            Result.Success(new ProcessResult(0, string.Empty, string.Empty)));
        var service = new HttpsDevelopmentCertificateService(runner);

        var result = await service.CheckAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(HttpsDevelopmentCertificateState.ValidUntrusted));
            Assert.That(runner.Requests, Has.Count.EqualTo(2));
            Assert.That(
                string.Join('\u001F', runner.Requests[1].Arguments),
                Is.EqualTo("dev-certs\u001Fhttps\u001F--check"));
        });
    }

    [Test]
    public async Task MissingCertificateReturnsMissingState()
    {
        var runner = new SequenceProcessRunner(
            Result.Success(new ProcessResult(1, string.Empty, string.Empty)),
            Result.Success(new ProcessResult(1, string.Empty, string.Empty)));
        var service = new HttpsDevelopmentCertificateService(runner);

        var result = await service.CheckAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(HttpsDevelopmentCertificateState.Missing));
        });
    }

    [Test]
    public async Task TrustUsesStandardDotNetCommand()
    {
        var runner = new SequenceProcessRunner(
            Result.Success(new ProcessResult(0, string.Empty, string.Empty)));
        var service = new HttpsDevelopmentCertificateService(runner);

        var result = await service.TrustAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(runner.Requests[0].FileName, Is.EqualTo("dotnet"));
            Assert.That(
                string.Join('\u001F', runner.Requests[0].Arguments),
                Is.EqualTo("dev-certs\u001Fhttps\u001F--trust"));
        });
    }

    [Test]
    public async Task ProcessFailureMapsToStableOperationError()
    {
        var runner = new SequenceProcessRunner(
            Result.Failure<ProcessResult>(OperationError.Create("process.start.failed", "dotnet unavailable")));
        var service = new HttpsDevelopmentCertificateService(runner);

        var result = await service.CheckAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("dotnet.https-certificate.execution.failed"));
        });
    }

    private sealed class SequenceProcessRunner(params Result<ProcessResult>[] results) : IProcessRunner
    {
        private readonly Queue<Result<ProcessResult>> _results = new(results);

        public List<ProcessRequest> Requests { get; } = [];

        public Task<Result<ProcessResult>> RunAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.FromResult(_results.Dequeue());
        }
    }
}
