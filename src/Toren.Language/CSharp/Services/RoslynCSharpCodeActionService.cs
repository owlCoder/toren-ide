using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
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
        var root = compilationContext.ActiveTree.GetRoot(cancellationToken);
        return compilationContext.Compilation
            .GetDiagnostics(cancellationToken)
            .Where(diagnostic =>
                diagnostic.Location.IsInSource
                && ReferenceEquals(diagnostic.Location.SourceTree, compilationContext.ActiveTree)
                && IsOnLine(diagnostic.Location.SourceSpan.Start, line.Start, line.EndIncludingLineBreak))
            .SelectMany(diagnostic => CreateActions(compilationContext, root, diagnostic, cancellationToken))
            .DistinctBy(action => action.Id)
            .OrderBy(action => action.Edit.StartOffset)
            .ThenBy(action => action.Title, StringComparer.Ordinal)
            .ToArray();
    }

    private static Microsoft.CodeAnalysis.Text.TextLine GetLineForPosition(
        SourceText sourceText,
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
        RoslynCompilationContext compilationContext,
        SyntaxNode root,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var insertionAction = CreateInsertionAction(diagnostic);
        if (insertionAction is not null)
        {
            yield return insertionAction;
        }

        if (diagnostic.Id == UnnecessaryUsingDiagnosticId)
        {
            if (CreateRemoveUnnecessaryUsingAction(
                    compilationContext,
                    root,
                    diagnostic) is { } removeUsingAction)
            {
                yield return removeUsingAction;
            }

            yield break;
        }

        if (diagnostic.Id != TypeOrNamespaceNotFoundDiagnosticId
            || root is not CompilationUnitSyntax compilationUnit)
        {
            yield break;
        }

        foreach (var action in CreateAddUsingActions(
                     compilationContext,
                     compilationUnit,
                     diagnostic,
                     cancellationToken))
        {
            yield return action;
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

    private static IEnumerable<CSharpCodeActionInfo> CreateAddUsingActions(
        RoslynCompilationContext compilationContext,
        CompilationUnitSyntax compilationUnit,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var span = diagnostic.Location.SourceSpan;
        if (span.Start >= compilationContext.SourceText.Length)
        {
            yield break;
        }

        var token = compilationUnit.FindToken(span.Start);
        var simpleName = token.Parent?
            .AncestorsAndSelf()
            .OfType<SimpleNameSyntax>()
            .FirstOrDefault(candidate => candidate.Identifier.Span.IntersectsWith(span));
        if (simpleName is null)
        {
            yield break;
        }

        var identifier = simpleName.Identifier.ValueText;
        var arity = simpleName is GenericNameSyntax genericName
            ? genericName.TypeArgumentList.Arguments.Count
            : 0;
        var currentNamespace = compilationContext.SemanticModel
            .GetEnclosingSymbol(span.Start, cancellationToken)?
            .ContainingNamespace?
            .ToDisplayString() ?? string.Empty;
        var existingNamespaces = compilationUnit.Usings
            .Where(usingDirective => usingDirective.Alias is null && usingDirective.Name is not null)
            .Select(usingDirective => usingDirective.Name!.ToString())
            .ToHashSet(StringComparer.Ordinal);
        var namespaceNames = FindTypeCandidates(
                compilationContext.Compilation.GlobalNamespace,
                identifier,
                arity,
                cancellationToken)
            .Where(type => IsAccessibleUsingCandidate(compilationContext, type))
            .Select(type => type.ContainingNamespace.ToDisplayString())
            .Where(namespaceName =>
                !string.IsNullOrWhiteSpace(namespaceName)
                && !namespaceName.Equals(currentNamespace, StringComparison.Ordinal)
                && !existingNamespaces.Contains(namespaceName))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(namespaceName => namespaceName, StringComparer.Ordinal)
            .ToArray();

        foreach (var namespaceName in namespaceNames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var edit = CreateUsingEdit(compilationContext, compilationUnit, namespaceName);
            yield return new CSharpCodeActionInfo(
                $"csharp.add-using:{namespaceName}:{edit.StartOffset}",
                $"Add using {namespaceName}",
                diagnostic.Id,
                edit);
        }
    }

    private static IEnumerable<INamedTypeSymbol> FindTypeCandidates(
        INamespaceSymbol namespaceSymbol,
        string identifier,
        int arity,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var type in namespaceSymbol.GetTypeMembers(identifier, arity))
        {
            yield return type;
        }

        foreach (var childNamespace in namespaceSymbol.GetNamespaceMembers())
        {
            foreach (var type in FindTypeCandidates(
                         childNamespace,
                         identifier,
                         arity,
                         cancellationToken))
            {
                yield return type;
            }
        }
    }

    private static bool IsAccessibleUsingCandidate(
        RoslynCompilationContext compilationContext,
        INamedTypeSymbol type)
    {
        if (type.ContainingType is not null || type.ContainingNamespace.IsGlobalNamespace)
        {
            return false;
        }

        if (type.DeclaredAccessibility == Accessibility.Public)
        {
            return true;
        }

        return type.DeclaredAccessibility == Accessibility.Internal
            && SymbolEqualityComparer.Default.Equals(
                type.ContainingAssembly,
                compilationContext.Compilation.Assembly);
    }

    private static CSharpTextEdit CreateUsingEdit(
        RoslynCompilationContext compilationContext,
        CompilationUnitSyntax compilationUnit,
        string namespaceName)
    {
        var editOffset = GetUsingInsertionOffset(compilationUnit);
        var newLine = GetPreferredNewLine(compilationContext.SourceText);
        var needsLeadingNewLine = editOffset > 0
            && compilationContext.SourceText[editOffset - 1] is not '\r' and not '\n';
        var newText = $"{(needsLeadingNewLine ? newLine : string.Empty)}using {namespaceName};{newLine}";
        return new CSharpTextEdit(
            compilationContext.ActiveTree.FilePath,
            editOffset,
            0,
            newText);
    }

    private static int GetUsingInsertionOffset(CompilationUnitSyntax compilationUnit)
    {
        if (compilationUnit.Usings.Count > 0)
        {
            return compilationUnit.Usings[^1].FullSpan.End;
        }

        if (compilationUnit.Externs.Count > 0)
        {
            return compilationUnit.Externs[^1].FullSpan.End;
        }

        if (compilationUnit.AttributeLists.Count > 0)
        {
            return compilationUnit.AttributeLists[0].SpanStart;
        }

        return compilationUnit.Members.Count > 0
            ? compilationUnit.Members[0].SpanStart
            : 0;
    }

    private static string GetPreferredNewLine(SourceText sourceText)
    {
        foreach (var line in sourceText.Lines)
        {
            if (line.EndIncludingLineBreak > line.End)
            {
                return sourceText.ToString(TextSpan.FromBounds(line.End, line.EndIncludingLineBreak));
            }
        }

        return Environment.NewLine;
    }

    private static CSharpCodeActionInfo? CreateRemoveUnnecessaryUsingAction(
        RoslynCompilationContext compilationContext,
        SyntaxNode root,
        Diagnostic diagnostic)
    {
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

        var line = compilationContext.SourceText.Lines.GetLineFromPosition(usingDirective.SpanStart);
        var beforeUsing = compilationContext.SourceText.ToString(
            TextSpan.FromBounds(line.Start, usingDirective.SpanStart));
        var afterUsing = compilationContext.SourceText.ToString(
            TextSpan.FromBounds(usingDirective.Span.End, line.End));

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
                && char.IsWhiteSpace(compilationContext.SourceText[usingDirective.Span.End]))
            {
                length++;
            }
        }

        return new CSharpCodeActionInfo(
            $"csharp.remove-unnecessary-using:{startOffset}:{length}",
            "Remove unnecessary using",
            diagnostic.Id,
            new CSharpTextEdit(
                compilationContext.ActiveTree.FilePath,
                startOffset,
                length,
                string.Empty));
    }

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
