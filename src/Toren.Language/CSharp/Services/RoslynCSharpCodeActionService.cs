using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Toren.Language.CSharp.Contracts;
using Toren.Language.CSharp.Models;

namespace Toren.Language.CSharp.Services;

public sealed class RoslynCSharpCodeActionService : ICSharpCodeActionService
{
    private const string MissingSemicolonDiagnosticId = "CS1002";
    private const string MissingClosingParenthesisDiagnosticId = "CS1026";
    private const string MissingClosingBraceDiagnosticId = "CS1513";
    private const string TypeOrNamespaceNotFoundDiagnosticId = "CS0246";
    private const string UnnecessaryUsingDiagnosticId = "CS8019";

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
            .SelectMany(diagnostic => CreateActions(compilationContext, diagnostic, cancellationToken))
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

    private static IEnumerable<CSharpCodeActionInfo> CreateActions(
        RoslynCompilationContext context,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var insertionAction = CreateInsertionAction(diagnostic);
        if (insertionAction is not null)
        {
            yield return insertionAction;
            yield break;
        }

        if (diagnostic.Id == TypeOrNamespaceNotFoundDiagnosticId)
        {
            foreach (var action in CreateAddUsingActions(context, diagnostic, cancellationToken))
            {
                yield return action;
            }

            yield break;
        }

        if (diagnostic.Id == UnnecessaryUsingDiagnosticId
            && CreateRemoveUnnecessaryUsingAction(context, diagnostic, cancellationToken) is { } removeUsingAction)
        {
            yield return removeUsingAction;
        }
    }

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

    private static IEnumerable<CSharpCodeActionInfo> CreateAddUsingActions(
        RoslynCompilationContext context,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var root = context.ActiveTree.GetRoot(cancellationToken);
        var diagnosticNode = root.FindNode(
            diagnostic.Location.SourceSpan,
            getInnermostNodeForTie: true);
        var simpleName = diagnosticNode
            .AncestorsAndSelf()
            .OfType<SimpleNameSyntax>()
            .FirstOrDefault();
        if (simpleName is null)
        {
            yield break;
        }

        var identifier = simpleName.Identifier.ValueText;
        if (string.IsNullOrWhiteSpace(identifier))
        {
            yield break;
        }

        var namespaces = context.Compilation
            .GetSymbolsWithName(identifier, SymbolFilter.Type, cancellationToken)
            .OfType<INamedTypeSymbol>()
            .Where(symbol => symbol.CanBeReferencedByName)
            .Select(symbol => symbol.ContainingNamespace?.ToDisplayString())
            .Where(namespaceName => !string.IsNullOrWhiteSpace(namespaceName))
            .Select(namespaceName => namespaceName!)
            .Where(namespaceName => !NamespaceAlreadyImported(root, namespaceName))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(namespaceName => namespaceName, StringComparer.Ordinal)
            .ToArray();

        foreach (var namespaceName in namespaces)
        {
            var edit = CreateUsingInsertionEdit(context, root, namespaceName);
            yield return new CSharpCodeActionInfo(
                $"csharp.add-using:{namespaceName}:{diagnostic.Location.SourceSpan.Start}",
                $"Add using {namespaceName}",
                diagnostic.Id,
                edit);
        }
    }

    private static bool NamespaceAlreadyImported(SyntaxNode root, string namespaceName) =>
        root.DescendantNodes()
            .OfType<UsingDirectiveSyntax>()
            .Any(usingDirective =>
                usingDirective.Alias is null
                && usingDirective.StaticKeyword.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.None)
                && usingDirective.Name?.ToString().Equals(namespaceName, StringComparison.Ordinal) == true);

    private static CSharpTextEdit CreateUsingInsertionEdit(
        RoslynCompilationContext context,
        SyntaxNode root,
        string namespaceName)
    {
        var compilationUnit = (CompilationUnitSyntax)root;
        var newline = DetectNewline(context.SourceText.ToString());
        var existingUsings = compilationUnit.Usings;
        if (existingUsings.Count > 0)
        {
            var lastUsing = existingUsings[^1];
            var insertionOffset = lastUsing.FullSpan.End;
            return new CSharpTextEdit(
                context.ActiveTree.FilePath,
                insertionOffset,
                0,
                $"using {namespaceName};{newline}");
        }

        var firstMember = compilationUnit.Members.FirstOrDefault();
        var offset = firstMember?.FullSpan.Start ?? 0;
        return new CSharpTextEdit(
            context.ActiveTree.FilePath,
            offset,
            0,
            $"using {namespaceName};{newline}{(offset == 0 ? string.Empty : newline)}");
    }

    private static CSharpCodeActionInfo? CreateRemoveUnnecessaryUsingAction(
        RoslynCompilationContext context,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var root = context.ActiveTree.GetRoot(cancellationToken);
        var diagnosticNode = root.FindNode(
            diagnostic.Location.SourceSpan,
            getInnermostNodeForTie: true);
        var usingDirective = diagnosticNode
            .AncestorsAndSelf()
            .OfType<UsingDirectiveSyntax>()
            .FirstOrDefault();
        if (usingDirective is null)
        {
            return null;
        }

        var line = context.SourceText.Lines.GetLineFromPosition(usingDirective.SpanStart);
        var beforeUsing = context.SourceText.ToString(
            Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(line.Start, usingDirective.SpanStart));
        var afterUsing = context.SourceText.ToString(
            Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(usingDirective.Span.End, line.End));

        int startOffset;
        int length;
        if (string.IsNullOrWhiteSpace(beforeUsing) && string.IsNullOrWhiteSpace(afterUsing))
        {
            startOffset = line.Start;
            length = line.EndIncludingLineBreak - line.Start;
        }
        else
        {
            startOffset = usingDirective.SpanStart;
            length = usingDirective.Span.Length;
            if (usingDirective.Span.End < line.End
                && char.IsWhiteSpace(context.SourceText[usingDirective.Span.End]))
            {
                length++;
            }
        }

        return new CSharpCodeActionInfo(
            $"csharp.remove-unnecessary-using:{startOffset}:{length}",
            "Remove unnecessary using",
            diagnostic.Id,
            new CSharpTextEdit(
                context.ActiveTree.FilePath,
                startOffset,
                length,
                string.Empty));
    }

    private static string DetectNewline(string text) =>
        text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
}
