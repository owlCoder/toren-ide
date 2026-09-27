using Toren.Core.Execution.Models;

namespace Toren.Workspaces.Adapters;

internal static class ProcessFailureDetails
{
    public static string From(ProcessResult processResult)
    {
        ArgumentNullException.ThrowIfNull(processResult);

        var details = string.IsNullOrWhiteSpace(processResult.StandardError)
            ? processResult.StandardOutput.Trim()
            : processResult.StandardError.Trim();
        return string.IsNullOrWhiteSpace(details)
            ? "The .NET CLI did not provide error details."
            : details;
    }
}
