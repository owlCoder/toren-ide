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
            $"Request {requestNumber} has an invalid absolute URI: {value}");

    public static OperationError InvalidHeader(int requestNumber, string line) =>
        OperationError.Create(
            "http.request.header.invalid",
            $"Request {requestNumber} has an invalid header: {line}");

    public static OperationError ExecutionFailed(string details) =>
        OperationError.Create("http.request.execution.failed", $"HTTP request failed. {details}");
}
