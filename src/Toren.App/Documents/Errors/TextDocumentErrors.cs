using Toren.Core.Results;

namespace Toren.App.Documents.Errors;

internal static class TextDocumentErrors
{
    public static OperationError NotFound(string path) =>
        OperationError.Create("document.not-found", $"Document was not found: {path}");

    public static OperationError ReadFailed(string path, string details) =>
        OperationError.Create("document.read.failed", $"Could not read '{path}': {details}");

    public static OperationError WriteFailed(string path, string details) =>
        OperationError.Create("document.write.failed", $"Could not save '{path}': {details}");

    public static OperationError UnsupportedEncoding(string path) =>
        OperationError.Create(
            "document.encoding.unsupported",
            $"'{path}' is not valid UTF-8/UTF-16 text and cannot be opened safely.");

    public static OperationError BinaryFile(string path) =>
        OperationError.Create(
            "document.binary.unsupported",
            $"'{path}' appears to be a binary file and cannot be opened in the text editor.");
}
