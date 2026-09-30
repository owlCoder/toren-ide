using Toren.Core.Results;
using Toren.Http.Contracts;
using Toren.Http.Models;

namespace Toren.Http.Services;

public sealed class HttpRequestFileParser : IHttpRequestFileParser
{
    private const string InvalidDocumentCode = "http.file.invalid";

    public Result<HttpRequestFile> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var requests = new List<HttpRequestDefinition>();
        var index = 0;
        string? pendingName = null;

        while (index < lines.Length)
        {
            var trimmed = lines[index].Trim();
            if (string.IsNullOrEmpty(trimmed) || IsComment(trimmed))
            {
                index++;
                continue;
            }

            if (trimmed.StartsWith("###", StringComparison.Ordinal))
            {
                pendingName = trimmed[3..].Trim();
                if (pendingName.Length == 0)
                {
                    pendingName = null;
                }

                index++;
                continue;
            }

            if (trimmed.StartsWith('@'))
            {
                var separator = trimmed.IndexOf('=');
                if (separator <= 1)
                {
                    return Invalid($"Invalid variable declaration on line {index + 1}.");
                }

                var key = trimmed[1..separator].Trim();
                var value = trimmed[(separator + 1)..].Trim();
                if (key.Length == 0)
                {
                    return Invalid($"Variable name is missing on line {index + 1}.");
                }

                variables[key] = value;
                index++;
                continue;
            }

            var requestLine = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (requestLine.Length < 2)
            {
                return Invalid($"Invalid request line {index + 1}.");
            }

            var method = requestLine[0];
            var url = requestLine[1];
            index++;

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            while (index < lines.Length)
            {
                var headerLine = lines[index];
                var headerTrimmed = headerLine.Trim();
                if (headerTrimmed.StartsWith("###", StringComparison.Ordinal))
                {
                    break;
                }

                if (headerTrimmed.Length == 0)
                {
                    index++;
                    break;
                }

                if (IsComment(headerTrimmed))
                {
                    index++;
                    continue;
                }

                var colon = headerLine.IndexOf(':');
                if (colon <= 0)
                {
                    break;
                }

                var name = headerLine[..colon].Trim();
                var value = headerLine[(colon + 1)..].Trim();
                headers[name] = value;
                index++;
            }

            var bodyLines = new List<string>();
            while (index < lines.Length && !lines[index].TrimStart().StartsWith("###", StringComparison.Ordinal))
            {
                bodyLines.Add(lines[index]);
                index++;
            }

            while (bodyLines.Count > 0 && bodyLines[^1].Length == 0)
            {
                bodyLines.RemoveAt(bodyLines.Count - 1);
            }

            requests.Add(new HttpRequestDefinition(
                pendingName,
                method,
                url,
                headers,
                bodyLines.Count == 0 ? null : string.Join('\n', bodyLines)));
            pendingName = null;
        }

        if (requests.Count == 0)
        {
            return Invalid("The HTTP file does not contain any requests.");
        }

        return Result.Success(new HttpRequestFile(variables, requests));
    }

    private static bool IsComment(string text) =>
        text.StartsWith('#') && !text.StartsWith("###", StringComparison.Ordinal)
        || text.StartsWith("//", StringComparison.Ordinal);

    private static Result<HttpRequestFile> Invalid(string message) =>
        Result.Failure<HttpRequestFile>(new OperationError(InvalidDocumentCode, message));
}
