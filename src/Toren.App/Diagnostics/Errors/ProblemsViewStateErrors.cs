using Toren.Core.Results;

namespace Toren.App.Diagnostics.Errors;

internal static class ProblemsViewStateErrors
{
    public static OperationError InvalidFormat() =>
        OperationError.Create("problems.view-state.invalid", "Problems view-state data is invalid.");

    public static OperationError ReadFailed(string details) =>
        OperationError.Create("problems.view-state.read.failed", $"Could not read Problems view state: {details}");

    public static OperationError WriteFailed(string details) =>
        OperationError.Create("problems.view-state.write.failed", $"Could not save Problems view state: {details}");
}
