using Toren.App.EnvironmentDoctor.Contracts;
using Toren.App.EnvironmentDoctor.Models;
using Toren.Containers.Contracts;
using Toren.Core.Results;
using Toren.DotNet.AspNetCore.Contracts;
using Toren.DotNet.AspNetCore.Models;
using Toren.DotNet.Environment.Contracts;
using Toren.Git.Contracts;

namespace Toren.App.EnvironmentDoctor.Services;

public sealed class EnvironmentDoctorService(
    IDotNetSdkResolver sdkResolver,
    IGitEnvironmentService gitEnvironmentService,
    IDockerComposeService dockerComposeService,
    IHttpsDevelopmentCertificateService httpsCertificateService) : IEnvironmentDoctorService
{
    private readonly IDotNetSdkResolver _sdkResolver = sdkResolver
        ?? throw new ArgumentNullException(nameof(sdkResolver));
    private readonly IGitEnvironmentService _gitEnvironmentService = gitEnvironmentService
        ?? throw new ArgumentNullException(nameof(gitEnvironmentService));
    private readonly IDockerComposeService _dockerComposeService = dockerComposeService
        ?? throw new ArgumentNullException(nameof(dockerComposeService));
    private readonly IHttpsDevelopmentCertificateService _httpsCertificateService = httpsCertificateService
        ?? throw new ArgumentNullException(nameof(httpsCertificateService));

    public async Task<Result<EnvironmentDoctorReport>> CheckAsync(
        string workspacePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        var fullWorkspacePath = Path.GetFullPath(workspacePath);

        var sdk = await _sdkResolver.ResolveVersionAsync(fullWorkspacePath, cancellationToken)
            .ConfigureAwait(false);
        var git = await _gitEnvironmentService.GetVersionAsync(cancellationToken)
            .ConfigureAwait(false);
        var docker = await _dockerComposeService.DetectAsync(cancellationToken)
            .ConfigureAwait(false);
        var https = await _httpsCertificateService.CheckAsync(cancellationToken)
            .ConfigureAwait(false);

        EnvironmentDoctorCheck[] checks =
        [
            CreateSdkCheck(sdk),
            CreateGitCheck(git),
            CreateDockerCheck(docker),
            CreateHttpsCheck(https),
        ];

        return Result.Success(new EnvironmentDoctorReport(checks));
    }

    private static EnvironmentDoctorCheck CreateSdkCheck(Result<string> result) =>
        result.IsSuccess
            ? new EnvironmentDoctorCheck(
                "dotnet-sdk",
                ".NET SDK",
                EnvironmentDoctorStatus.Healthy,
                $"Workspace resolves to .NET SDK {result.Value}.")
            : new EnvironmentDoctorCheck(
                "dotnet-sdk",
                ".NET SDK",
                EnvironmentDoctorStatus.Error,
                result.Error.Message,
                "Install a compatible .NET SDK or update global.json to an installed version.");

    private static EnvironmentDoctorCheck CreateGitCheck(Result<string> result) =>
        result.IsSuccess
            ? new EnvironmentDoctorCheck(
                "git",
                "Git",
                EnvironmentDoctorStatus.Healthy,
                result.Value!)
            : new EnvironmentDoctorCheck(
                "git",
                "Git",
                EnvironmentDoctorStatus.Error,
                result.Error.Message,
                "Install Git and ensure the git executable is available on PATH.");

    private static EnvironmentDoctorCheck CreateDockerCheck(
        Result<Toren.Containers.Models.DockerComposeToolStatus> result)
    {
        if (result.IsFailure)
        {
            return new EnvironmentDoctorCheck(
                "docker-compose",
                "Docker Compose",
                EnvironmentDoctorStatus.Warning,
                result.Error.Message,
                "Install Docker Desktop or Docker Engine with the Compose plugin if container workflows are needed.");
        }

        var status = result.Value!;
        return status.IsAvailable
            ? new EnvironmentDoctorCheck(
                "docker-compose",
                "Docker Compose",
                EnvironmentDoctorStatus.Healthy,
                string.IsNullOrWhiteSpace(status.Version)
                    ? "Docker Compose is available."
                    : $"Docker Compose {status.Version} is available.")
            : new EnvironmentDoctorCheck(
                "docker-compose",
                "Docker Compose",
                EnvironmentDoctorStatus.Warning,
                status.Details ?? "Docker Compose is not available.",
                "Install Docker Desktop or Docker Engine with the Compose plugin if container workflows are needed.");
    }

    private static EnvironmentDoctorCheck CreateHttpsCheck(
        Result<HttpsDevelopmentCertificateState> result)
    {
        if (result.IsFailure)
        {
            return new EnvironmentDoctorCheck(
                "https-dev-cert",
                "ASP.NET Core HTTPS certificate",
                EnvironmentDoctorStatus.Warning,
                result.Error.Message,
                "Run 'dotnet dev-certs https --trust' to create and trust the development certificate.");
        }

        return result.Value switch
        {
            HttpsDevelopmentCertificateState.Trusted => new EnvironmentDoctorCheck(
                "https-dev-cert",
                "ASP.NET Core HTTPS certificate",
                EnvironmentDoctorStatus.Healthy,
                "A valid trusted HTTPS development certificate is available."),
            HttpsDevelopmentCertificateState.ValidUntrusted => new EnvironmentDoctorCheck(
                "https-dev-cert",
                "ASP.NET Core HTTPS certificate",
                EnvironmentDoctorStatus.Warning,
                "A valid HTTPS development certificate exists but is not trusted.",
                "Run 'dotnet dev-certs https --trust'."),
            _ => new EnvironmentDoctorCheck(
                "https-dev-cert",
                "ASP.NET Core HTTPS certificate",
                EnvironmentDoctorStatus.Warning,
                "No valid HTTPS development certificate was found.",
                "Run 'dotnet dev-certs https --trust'."),
        };
    }
}
