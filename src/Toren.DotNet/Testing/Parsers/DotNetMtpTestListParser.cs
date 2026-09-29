using System.Text.Json;
using Toren.DotNet.Testing.Models;

namespace Toren.DotNet.Testing.Parsers;

public static class DotNetMtpTestListParser
{
    public static IReadOnlyList<DotNetTestCase> Parse(string output)
    {
        ArgumentNullException.ThrowIfNull(output);

        using var document = ParseDocument(output);
        var root = document.RootElement;
        if (!root.TryGetProperty("schemaVersion", out var schemaVersion)
            || schemaVersion.ValueKind != JsonValueKind.Number
            || schemaVersion.GetInt32() != 1
            || !root.TryGetProperty("tests", out var testsElement)
            || testsElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Unsupported Microsoft Testing Platform test-list schema.");
        }

        var tests = new List<DotNetTestCase>();
        foreach (var testElement in testsElement.EnumerateArray())
        {
            var uid = GetRequiredString(testElement, "uid");
            var displayName = GetRequiredString(testElement, "displayName");
            tests.Add(new DotNetTestCase(
                BuildFullyQualifiedName(testElement, displayName),
                displayName,
                uid));
        }

        return tests;
    }

    private static JsonDocument ParseDocument(string output)
    {
        var end = output.LastIndexOf('}');
        if (end < 0)
        {
            throw new JsonException("Microsoft Testing Platform did not return JSON discovery output.");
        }

        var start = output.IndexOf('{');
        while (start >= 0 && start < end)
        {
            try
            {
                var document = JsonDocument.Parse(output[(start)..(end + 1)]);
                if (document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty("schemaVersion", out _))
                {
                    return document;
                }

                document.Dispose();
            }
            catch (JsonException)
            {
                // Build/test-host output may precede the structured discovery payload.
            }

            start = output.IndexOf('{', start + 1);
        }

        throw new JsonException("Microsoft Testing Platform did not return a supported JSON discovery payload.");
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new JsonException($"Microsoft Testing Platform discovery output is missing '{propertyName}'.");
        }

        return property.GetString()!;
    }

    private static string BuildFullyQualifiedName(JsonElement testElement, string displayName)
    {
        if (!testElement.TryGetProperty("type", out var typeElement)
            || typeElement.ValueKind != JsonValueKind.Object)
        {
            return displayName;
        }

        var parts = new List<string>(3);
        AddStringPart(typeElement, "namespace", parts);
        AddStringPart(typeElement, "typeName", parts);
        AddStringPart(typeElement, "methodName", parts);
        return parts.Count == 0
            ? displayName
            : string.Join('.', parts);
    }

    private static void AddStringPart(JsonElement element, string propertyName, List<string> parts)
    {
        if (element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(property.GetString()))
        {
            parts.Add(property.GetString()!);
        }
    }
}
