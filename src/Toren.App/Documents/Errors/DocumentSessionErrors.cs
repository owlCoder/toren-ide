using Toren.Core.Results;

namespace Toren.App.Documents.Errors;

internal static class DocumentSessionErrors
{
    public static OperationError InvalidFormat() =>
        OperationError.Create("document.session.invalid", "Document session data is invalid.");

    public static OperationError ReadFailed(string details) =>
        OperationError.Create("document.session.read.failed", $"Could not read document session data: {details}");

    public static OperationError WriteFailed(string details) =>
        OperationError.Create("document.session.write.failed", $"Could not save document session data: {details}");
}
