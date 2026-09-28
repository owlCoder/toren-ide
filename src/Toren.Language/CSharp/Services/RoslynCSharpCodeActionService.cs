using Microsoft.CodeAnalysis;
using Toren.Language.CSharp.Contracts;
using Toren.Language.CSharp.Models;

namespace Toren.Language.CSharp.Services;

public sealed class RoslynCSharpCodeActionService : ICSharpCodeActionService
{
    private const string MissingSemicolonDiagnosticId = "CS1002";
    private const string MissingClosingParenthesisDiagnosticId = "CS1026";
    private const string MissingClosingBraceDiagnosticId = "CS1513";

    public Task<IReadOnlyList<CSharpCodeActionInfo>> GetActionsAsync(
        CSharpSemanticContext context,
        int position,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegative(position);

        return Task.Run<IReadOnlyList<CSharpCodeActionInfo>>(
            () => GetActions(context, position, cancellationToken),
            cancellationToken);
    }

    private static CSharpCodeActionInfo[] GetActions(
        CSharpSemanticContext context,
        int position,
        CancellationToken cancellationToken)
    {
        var compilationContext = RoslynCompilationContextFactory.Create(context, cancellationToken);
        if (compilationContext is null)
        {
            return [];
        }

        ArgumentOutOfRangeException.ThrowIfGreaterThan(position, compilationContext.SourceText.Length);

        var line = GetLineForPosition(compilationContext.SourceText, position);
        return compilationContext.Compilation
            .GetDiagnostics(cancellationToken)
            .Where(diagnostic =>
                diagnostic.Location.IsInSource
                && ReferenceEquals(diagnostic.Location.SourceTree, compilationContext.ActiveTree)
                && IsOnLine(diagnostic.Location.SourceSpan.Start, line.Start, line.EndIncludingLineBreak))
            .Select(CreateInsertionAction)
            .Where(action => action is not null)
            .Select(action => action!)
            .DistinctBy(action => action.Id)
            .OrderBy(action => action.Edit.StartOffset)
            .ThenBy(action => action.Title, StringComparer.Ordinal)
            .ToArray();
    }

    private static Microsoft.CodeAnalysis.Text.TextLine GetLineForPosition(
        Microsoft.CodeAnalysis.Text.SourceText sourceText,
        int position)
    {
        if (sourceText.Length == 0)
        {
            return sourceText.Lines[0];
        }

        var lookupPosition = Math.Min(position, sourceText.Length - 1);
        return sourceText.Lines.GetLineFromPosition(lookupPosition);
    }

    private static bool IsOnLine(int diagnosticPosition, int lineStart, int lineEnd) =>
        diagnosticPosition >= lineStart && diagnosticPosition <= lineEnd;

    private static CSharpCodeActionInfo? CreateInsertionAction(Diagnostic diagnostic) =>
        diagnostic.Id switch
        {
            MissingSemicolonDiagnosticId => CreateInsertionAction(
                diagnostic,
                "csharp.insert-missing-semicolon",
                "Insert missing semicolon",
                ";"),
            MissingClosingParenthesisDiagnosticId => CreateInsertionAction(
                diagnostic,
                "csharp.insert-missing-closing-parenthesis",
                "Insert missing closing parenthesis",
                ")"),
            MissingClosingBraceDiagnosticId => CreateInsertionAction(
                diagnostic,
                "csharp.insert-missing-closing-brace",
                "Insert missing closing brace",
                "}"),
            _ => null,
        };

    private static CSharpCodeActionInfo CreateInsertionAction(
        Diagnostic diagnostic,
        string actionId,
        string title,
        string newText)
    {
        var editOffset = diagnostic.Location.SourceSpan.Start;
        return new CSharpCodeActionInfo(
            $"{actionId}:{editOffset}",
            title,
            diagnostic.Id,
            new CSharpTextEdit(
                diagnostic.Location.SourceTree?.FilePath ?? string.Empty,
                editOffset,
                0,
                newText));
    }
}
