using Toren.Core.Results;

namespace Toren.DotNet.Http.Errors;

internal static class HttpRequestErrors
{
    public static OperationError NoRequests() =>
        OperationError.Create("http.document.empty", "The HTTP document does not contain a request.");

    public static OperationError InvalidRequestLine(int requestNumber, string line) =>
        OperationError.Create(
            "http.request.line.invalid",
            $"Request {requestNumber} has an invalid request line: {line}");

    public static OperationError InvalidUri(int requestNumber, string value) =>
        OperationError.Create(
            "http.request.uri.invalid",
            $"Request {requestNumber} has an invalid absolute HTTP URI: {value}");

    public static OperationError InvalidHeader(int requestNumber, string line) =>
        OperationError.Create(
            "http.request.header.invalid",
            $"Request {requestNumber} has an invalid header: {line}");

    public static OperationError MissingVariable(string name) =>
        OperationError.Create(
            "http.variable.missing",
            $"HTTP variable '{name}' is not defined in the selected environment.");

    public static OperationError InvalidVariablePlaceholder(string value) =>
        OperationError.Create(
            "http.variable.placeholder.invalid",
            $"HTTP variable placeholder is invalid: {value}");

    public static OperationError InvalidResolvedUri(string value) =>
        OperationError.Create(
            "http.variable.uri.invalid",
            $"Resolved HTTP request URI is invalid: {value}");

    public static OperationError ExecutionFailed(string details) =>
        OperationError.Create("http.request.execution.failed", $"HTTP request failed. {details}");
}
