using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;

namespace Toren.Platform.Execution.Adapters;

public sealed class SystemNativeShellProvider : INativeShellProvider
{
    public ProcessRequest CreateShellRequest(string workingDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        var shell = OperatingSystem.IsWindows()
            ? Environment.GetEnvironmentVariable("COMSPEC")
            : Environment.GetEnvironmentVariable("SHELL");
        if (string.IsNullOrWhiteSpace(shell))
        {
            shell = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh";
        }

        return new ProcessRequest(shell, [], Path.GetFullPath(workingDirectory));
    }
}
