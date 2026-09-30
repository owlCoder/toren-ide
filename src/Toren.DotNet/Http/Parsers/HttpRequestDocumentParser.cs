using Toren.Core.Results;
using Toren.DotNet.Http.Contracts;
using Toren.DotNet.Http.Errors;
using Toren.DotNet.Http.Models;

namespace Toren.DotNet.Http.Parsers;

public sealed class HttpRequestDocumentParser : IHttpRequestDocumentParser
{
    public Result<IReadOnlyList<HttpRequestDefinition>> Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var requests = new List<HttpRequestDefinition>();
        var requestNumber = 0;
        foreach (var segment in SplitSegments(content))
        {
            if (!HasRequestContent(segment))
            {
                continue;
            }

            requestNumber++;
            var parsed = ParseSegment(segment, requestNumber);
            if (parsed.IsFailure)
            {
                return Result.Failure<IReadOnlyList<HttpRequestDefinition>>(parsed.Error);
            }

            requests.Add(parsed.Value!);
        }

        return requests.Count == 0
            ? Result.Failure<IReadOnlyList<HttpRequestDefinition>>(HttpRequestErrors.NoRequests())
            : Result.Success<IReadOnlyList<HttpRequestDefinition>>(requests);
    }

    private static List<List<string>> SplitSegments(string content)
    {
        var normalized = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var segments = new List<List<string>>();
        var current = new List<string>();
        foreach (var line in normalized.Split('\n'))
        {
            if (string.Equals(line.Trim(), "###", StringComparison.Ordinal))
            {
                segments.Add(current);
                current = new List<string>();
                continue;
            }

            current.Add(line);
        }

        segments.Add(current);
        return segments;
    }

    private static bool HasRequestContent(List<string> lines) =>
        lines.Any(static line => !string.IsNullOrWhiteSpace(line) && !IsComment(line));

    private static Result<HttpRequestDefinition> ParseSegment(List<string> lines, int requestNumber)
    {
        var index = 0;
        string? name = null;
        while (index < lines.Count)
        {
            var line = lines[index];
            if (TryReadName(line, out var requestName))
            {
                name = requestName;
                index++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(line) || IsComment(line))
            {
                index++;
                continue;
            }

            break;
        }

        if (index >= lines.Count)
        {
            return Result.Failure<HttpRequestDefinition>(HttpRequestErrors.NoRequests());
        }

        var requestLine = lines[index].Trim();
        var whitespaceIndex = FindWhitespace(requestLine);
        if (whitespaceIndex <= 0 || whitespaceIndex >= requestLine.Length - 1)
        {
            return Result.Failure<HttpRequestDefinition>(
                HttpRequestErrors.InvalidRequestLine(requestNumber, requestLine));
        }

        var method = requestLine[..whitespaceIndex].Trim();
        var uriText = requestLine[whitespaceIndex..].Trim();
        if (method.Length == 0 || method.Any(static character => !char.IsLetter(character)))
        {
            return Result.Failure<HttpRequestDefinition>(
                HttpRequestErrors.InvalidRequestLine(requestNumber, requestLine));
        }

        if (!Uri.TryCreate(uriText, UriKind.Absolute, out var uri))
        {
            return Result.Failure<HttpRequestDefinition>(HttpRequestErrors.InvalidUri(requestNumber, uriText));
        }

        index++;
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var bodyStart = -1;
        for (; index < lines.Count; index++)
        {
            var line = lines[index];
            if (string.IsNullOrWhiteSpace(line))
            {
                bodyStart = index + 1;
                break;
            }

            if (IsComment(line))
            {
                continue;
            }

            var colonIndex = line.IndexOf(':');
            if (colonIndex <= 0)
            {
                return Result.Failure<HttpRequestDefinition>(
                    HttpRequestErrors.InvalidHeader(requestNumber, line.Trim()));
            }

            var headerName = line[..colonIndex].Trim();
            if (headerName.Length == 0)
            {
                return Result.Failure<HttpRequestDefinition>(
                    HttpRequestErrors.InvalidHeader(requestNumber, line.Trim()));
            }

            headers[headerName] = line[(colonIndex + 1)..].Trim();
        }

        string? body = null;
        if (bodyStart >= 0 && bodyStart < lines.Count)
        {
            var bodyText = string.Join(System.Environment.NewLine, lines.Skip(bodyStart)).TrimEnd();
            if (bodyText.Length > 0)
            {
                body = bodyText;
            }
        }

        return Result.Success(new HttpRequestDefinition(
            method.ToUpperInvariant(),
            uri,
            headers,
            body,
            name));
    }

    private static int FindWhitespace(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (char.IsWhiteSpace(value[index]))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsComment(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith('#') || trimmed.StartsWith("//", StringComparison.Ordinal);
    }

    private static bool TryReadName(string line, out string? name)
    {
        var trimmed = line.Trim();
        const string hashPrefix = "# @name ";
        const string slashPrefix = "// @name ";
        string? value = null;
        if (trimmed.StartsWith(hashPrefix, StringComparison.OrdinalIgnoreCase))
        {
            value = trimmed[hashPrefix.Length..].Trim();
        }
        else if (trimmed.StartsWith(slashPrefix, StringComparison.OrdinalIgnoreCase))
        {
            value = trimmed[slashPrefix.Length..].Trim();
        }

        name = string.IsNullOrWhiteSpace(value) ? null : value;
        return name is not null;
    }
}
