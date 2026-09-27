using System.ComponentModel;
using Toren.Core.Execution;

namespace Toren.DotNet.Environment;

public sealed class DotNetEnvironmentService(IProcessRunner processRunner) : IDotNetEnvironmentService
{
    private readonly IProcessRunner _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));

    public async Task<IReadOnlyList<DotNetSdkInfo>> GetInstalledSdksAsync(
        CancellationToken cancellationToken = default)
    {
        ProcessResult result;

        try
        {
            result = await _processRunner.RunAsync(
                ProcessRequest.Create("dotnet", "--list-sdks"),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Win32Exception exception)
        {
            throw new DotNetToolchainException(
                "The dotnet executable could not be started. Ensure a supported .NET SDK is installed and available on PATH.",
                exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new DotNetToolchainException(
                "The dotnet executable could not be started.",
                exception);
        }

        if (!result.Succeeded)
        {
            var details = string.IsNullOrWhiteSpace(result.StandardError)
                ? "No additional error output was provided."
                : result.StandardError.Trim();

            throw new DotNetToolchainException(
                $"dotnet --list-sdks exited with code {result.ExitCode}. {details}");
        }

        return DotNetSdkListParser.Parse(result.StandardOutput);
    }
}
