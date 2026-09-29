using System.Text.Json;
using Toren.Core.Results;
using Toren.Debugging.Models;

namespace Toren.Debugging.Services;

internal static class DapDebugInspectionParser
{
    private const string InvalidStackTraceErrorCode = "debug.session.invalid-stack-trace";
    private const string InvalidScopesErrorCode = "debug.session.invalid-scopes";
    private const string InvalidVariablesErrorCode = "debug.session.invalid-variables";
    private const string InvalidEvaluationErrorCode = "debug.session.invalid-evaluation";

    public static Result<IReadOnlyList<DebugStackFrame>> ParseStackTrace(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        if (!TryGetBodyArray(document.RootElement, "stackFrames", out var framesElement))
        {
            return Failure<IReadOnlyList<DebugStackFrame>>(
                InvalidStackTraceErrorCode,
                "The debug adapter returned an invalid stackTrace response.");
        }

        var frames = new List<DebugStackFrame>();
        foreach (var element in framesElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object
                || !TryGetPositiveInt32(element, "id", out var id)
                || !TryGetRequiredString(element, "name", out var name))
            {
                return Failure<IReadOnlyList<DebugStackFrame>>(
                    InvalidStackTraceErrorCode,
                    "The debug adapter returned an invalid stack frame.");
            }

            string? sourcePath = null;
            if (element.TryGetProperty("source", out var source)
                && source.ValueKind == JsonValueKind.Object
                && source.TryGetProperty("path", out var path)
                && path.ValueKind == JsonValueKind.String)
            {
                sourcePath = path.GetString();
            }

            frames.Add(new DebugStackFrame(
                id,
                name!,
                sourcePath,
                GetOptionalPositiveInt32(element, "line"),
                GetOptionalPositiveInt32(element, "column")));
        }

        return Result.Success<IReadOnlyList<DebugStackFrame>>(frames);
    }

    public static Result<IReadOnlyList<DebugScope>> ParseScopes(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        if (!TryGetBodyArray(document.RootElement, "scopes", out var scopesElement))
        {
            return Failure<IReadOnlyList<DebugScope>>(
                InvalidScopesErrorCode,
                "The debug adapter returned an invalid scopes response.");
        }

        var scopes = new List<DebugScope>();
        foreach (var element in scopesElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object
                || !TryGetRequiredString(element, "name", out var name)
                || !TryGetNonNegativeInt32(element, "variablesReference", out var variablesReference))
            {
                return Failure<IReadOnlyList<DebugScope>>(
                    InvalidScopesErrorCode,
                    "The debug adapter returned an invalid scope.");
            }

            var isExpensive = element.TryGetProperty("expensive", out var expensive)
                && expensive.ValueKind is JsonValueKind.True or JsonValueKind.False
                && expensive.GetBoolean();
            scopes.Add(new DebugScope(name!, variablesReference, isExpensive));
        }

        return Result.Success<IReadOnlyList<DebugScope>>(scopes);
    }

    public static Result<IReadOnlyList<DebugVariable>> ParseVariables(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        if (!TryGetBodyArray(document.RootElement, "variables", out var variablesElement))
        {
            return Failure<IReadOnlyList<DebugVariable>>(
                InvalidVariablesErrorCode,
                "The debug adapter returned an invalid variables response.");
        }

        var variables = new List<DebugVariable>();
        foreach (var element in variablesElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object
                || !TryGetRequiredString(element, "name", out var name)
                || !TryGetRequiredString(element, "value", out var value))
            {
                return Failure<IReadOnlyList<DebugVariable>>(
                    InvalidVariablesErrorCode,
                    "The debug adapter returned an invalid variable.");
            }

            var type = element.TryGetProperty("type", out var typeProperty)
                && typeProperty.ValueKind == JsonValueKind.String
                    ? typeProperty.GetString()
                    : null;
            var variablesReference = element.TryGetProperty("variablesReference", out var referenceProperty)
                && referenceProperty.TryGetInt32(out var parsedReference)
                && parsedReference >= 0
                    ? parsedReference
                    : 0;
            variables.Add(new DebugVariable(name!, value!, type, variablesReference));
        }

        return Result.Success<IReadOnlyList<DebugVariable>>(variables);
    }

    public static Result<DebugEvaluationResult> ParseEvaluation(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        if (!root.TryGetProperty("body", out var body)
            || body.ValueKind != JsonValueKind.Object
            || !TryGetRequiredString(body, "result", out var value))
        {
            return Failure<DebugEvaluationResult>(
                InvalidEvaluationErrorCode,
                "The debug adapter returned an invalid evaluate response.");
        }

        var type = body.TryGetProperty("type", out var typeProperty)
            && typeProperty.ValueKind == JsonValueKind.String
                ? typeProperty.GetString()
                : null;
        var variablesReference = body.TryGetProperty("variablesReference", out var referenceProperty)
            && referenceProperty.TryGetInt32(out var parsedReference)
            && parsedReference >= 0
                ? parsedReference
                : 0;
        return Result.Success(new DebugEvaluationResult(value!, type, variablesReference));
    }

    private static bool TryGetBodyArray(
        JsonElement root,
        string propertyName,
        out JsonElement array)
    {
        if (root.TryGetProperty("body", out var body)
            && body.ValueKind == JsonValueKind.Object
            && body.TryGetProperty(propertyName, out array)
            && array.ValueKind == JsonValueKind.Array)
        {
            return true;
        }

        array = default;
        return false;
    }

    private static bool TryGetRequiredString(
        JsonElement element,
        string propertyName,
        out string? value)
    {
        if (element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString();
            return value is not null;
        }

        value = null;
        return false;
    }

    private static bool TryGetPositiveInt32(
        JsonElement element,
        string propertyName,
        out int value)
    {
        value = 0;
        return element.TryGetProperty(propertyName, out var property)
            && property.TryGetInt32(out value)
            && value > 0;
    }

    private static bool TryGetNonNegativeInt32(
        JsonElement element,
        string propertyName,
        out int value)
    {
        value = 0;
        return element.TryGetProperty(propertyName, out var property)
            && property.TryGetInt32(out value)
            && value >= 0;
    }

    private static int? GetOptionalPositiveInt32(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property)
        && property.TryGetInt32(out var value)
        && value > 0
            ? value
            : null;

    private static Result<T> Failure<T>(string code, string message) =>
        Result.Failure<T>(OperationError.Create(code, message));
}
