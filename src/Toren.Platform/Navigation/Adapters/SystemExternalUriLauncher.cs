using System.ComponentModel;
using System.Diagnostics;
using Toren.Core.Navigation.Contracts;
using Toren.Core.Results;

namespace Toren.Platform.Navigation.Adapters;

public sealed class SystemExternalUriLauncher : IExternalUriLauncher
{
    private const string LaunchFailedErrorCode = "external-uri.launch.failed";

    public Result<bool> Launch(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri)
        {
            return Result.Failure<bool>(
                OperationError.Create(LaunchFailedErrorCode, "External URI must be absolute."));
        }

        try
        {
            using var process = Process.Start(
                new ProcessStartInfo(uri.AbsoluteUri)
                {
                    UseShellExecute = true,
                });
            return process is null
                ? Result.Failure<bool>(
                    OperationError.Create(
                        LaunchFailedErrorCode,
                        $"Unable to open '{uri.AbsoluteUri}'."))
                : Result.Success(true);
        }
        catch (Win32Exception exception)
        {
            return Result.Failure<bool>(
                OperationError.Create(
                    LaunchFailedErrorCode,
                    $"Unable to open '{uri.AbsoluteUri}': {exception.Message}"));
        }
        catch (InvalidOperationException exception)
        {
            return Result.Failure<bool>(
                OperationError.Create(
                    LaunchFailedErrorCode,
                    $"Unable to open '{uri.AbsoluteUri}': {exception.Message}"));
        }
    }
}
