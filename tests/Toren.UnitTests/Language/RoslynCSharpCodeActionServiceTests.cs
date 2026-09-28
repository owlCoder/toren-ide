using NUnit.Framework;
using Toren.Language.CSharp.Models;
using Toren.Language.CSharp.Services;

namespace Toren.UnitTests.Language;

[TestFixture]
public sealed class RoslynCSharpCodeActionServiceTests
{
    [Test]
    public async Task MissingSemicolonOnCaretLineReturnsInsertAction()
    {
        var service = new RoslynCSharpCodeActionService();
        const string source = "namespace Demo;\npublic sealed class Sample\n{\n    public int Value => 42\n}";
        var context = new CSharpSemanticContext(
            "Sample.cs",
            [new CSharpSourceDocument("Sample.cs", source)]);
        var position = source.IndexOf("42", StringComparison.Ordinal);

        var actions = await service.GetActionsAsync(context, position);

        Assert.Multiple(() =>
        {
            Assert.That(actions, Has.Count.EqualTo(1));
            Assert.That(actions[0].DiagnosticId, Is.EqualTo("CS1002"));
            Assert.That(actions[0].Title, Is.EqualTo("Insert missing semicolon"));
            Assert.That(actions[0].Edit.NewText, Is.EqualTo(";"));
            Assert.That(actions[0].Edit.Length, Is.Zero);
            Assert.That(actions[0].Edit.StartOffset, Is.EqualTo(source.IndexOf('\n', position)));
        });
    }

    [Test]
    public async Task MissingClosingParenthesisOnCaretLineReturnsInsertAction()
    {
        var service = new RoslynCSharpCodeActionService();
        const string source = "namespace Demo;\npublic sealed class Sample\n{\n    public bool Value => (42 > 0;\n}";
        var context = new CSharpSemanticContext(
            "Sample.cs",
            [new CSharpSourceDocument("Sample.cs", source)]);
        var position = source.IndexOf("42", StringComparison.Ordinal);

        var actions = await service.GetActionsAsync(context, position);
        var action = actions.Single(candidate => candidate.DiagnosticId == "CS1026");

        Assert.Multiple(() =>
        {
            Assert.That(action.Title, Is.EqualTo("Insert missing closing parenthesis"));
            Assert.That(action.Edit.NewText, Is.EqualTo(")"));
            Assert.That(action.Edit.Length, Is.Zero);
            Assert.That(action.Edit.StartOffset, Is.EqualTo(source.IndexOf(';', position)));
        });
    }

    [Test]
    public async Task MissingClosingBraceAtEndOfFileReturnsInsertAction()
    {
        var service = new RoslynCSharpCodeActionService();
        const string source = "namespace Demo;\npublic sealed class Sample\n{\n    public int Value => 42;\n";
        var context = new CSharpSemanticContext(
            "Sample.cs",
            [new CSharpSourceDocument("Sample.cs", source)]);

        var actions = await service.GetActionsAsync(context, source.Length);
        var action = actions.Single(candidate => candidate.DiagnosticId == "CS1513");

        Assert.Multiple(() =>
        {
            Assert.That(action.Title, Is.EqualTo("Insert missing closing brace"));
            Assert.That(action.Edit.NewText, Is.EqualTo("}"));
            Assert.That(action.Edit.Length, Is.Zero);
            Assert.That(action.Edit.StartOffset, Is.EqualTo(source.LastIndexOf('\n')));
        });
    }

    [Test]
    public async Task DifferentMissingTokensAtSameOffsetReturnDistinctActions()
    {
        var service = new RoslynCSharpCodeActionService();
        const string source = "namespace Demo;\npublic sealed class Sample\n{\n    public bool Value => (42 > 0\n}";
        var context = new CSharpSemanticContext(
            "Sample.cs",
            [new CSharpSourceDocument("Sample.cs", source)]);
        var position = source.IndexOf("42", StringComparison.Ordinal);

        var actions = await service.GetActionsAsync(context, position);

        Assert.Multiple(() =>
        {
            Assert.That(actions.Any(action => action.DiagnosticId == "CS1002"), Is.True);
            Assert.That(actions.Any(action => action.DiagnosticId == "CS1026"), Is.True);
        });
    }

    [Test]
    public async Task MissingFrameworkTypeReturnsAddUsingAction()
    {
        var service = new RoslynCSharpCodeActionService();
        const string source = "namespace Demo;\npublic sealed class Sample\n{\n    public StringBuilder Build() => new();\n}";
        var context = new CSharpSemanticContext(
            "Sample.cs",
            [new CSharpSourceDocument("Sample.cs", source)]);
        var position = source.IndexOf("StringBuilder", StringComparison.Ordinal);

        var actions = await service.GetActionsAsync(context, position);
        var action = actions.Single(candidate => candidate.Title == "Add using System.Text");

        Assert.Multiple(() =>
        {
            Assert.That(action.DiagnosticId, Is.EqualTo("CS0246"));
            Assert.That(action.Edit.StartOffset, Is.Zero);
            Assert.That(action.Edit.Length, Is.Zero);
            Assert.That(action.Edit.NewText, Is.EqualTo("using System.Text;\n"));
        });
    }

    [Test]
    public async Task MissingWorkspaceTypeReturnsAddUsingActionAfterExistingUsing()
    {
        var service = new RoslynCSharpCodeActionService();
        const string source = "using System;\nnamespace Consumer;\npublic sealed class Sample { public Widget Value { get; } = new(); }";
        const string dependency = "namespace Shared.Models; public sealed class Widget { }";
        var context = new CSharpSemanticContext(
            "Sample.cs",
            [
                new CSharpSourceDocument("Sample.cs", source),
                new CSharpSourceDocument("Widget.cs", dependency),
            ]);
        var position = source.IndexOf("Widget", StringComparison.Ordinal);

        var actions = await service.GetActionsAsync(context, position);
        var action = actions.Single(candidate => candidate.Title == "Add using Shared.Models");

        Assert.Multiple(() =>
        {
            Assert.That(action.Edit.StartOffset, Is.EqualTo("using System;\n".Length));
            Assert.That(action.Edit.NewText, Is.EqualTo("using Shared.Models;\n"));
        });
    }

    [Test]
    public async Task MissingTypeWithMultipleNamespacesReturnsCandidatePerNamespace()
    {
        var service = new RoslynCSharpCodeActionService();
        const string source = "namespace Consumer; public sealed class Sample { public Widget Value { get; } = new(); }";
        var context = new CSharpSemanticContext(
            "Sample.cs",
            [
                new CSharpSourceDocument("Sample.cs", source),
                new CSharpSourceDocument("One.cs", "namespace One; public sealed class Widget { }"),
                new CSharpSourceDocument("Two.cs", "namespace Two; public sealed class Widget { }"),
            ]);
        var position = source.IndexOf("Widget", StringComparison.Ordinal);

        var actions = await service.GetActionsAsync(context, position);
        var titles = actions.Select(action => action.Title).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(titles, Does.Contain("Add using One"));
            Assert.That(titles, Does.Contain("Add using Two"));
        });
    }

    [Test]
    public async Task UnnecessaryUsingReturnsWholeLineRemovalAction()
    {
        var service = new RoslynCSharpCodeActionService();
        const string source = "using System;\nusing System.Text;\nnamespace Demo;\npublic sealed class Sample { public DateTime Value { get; } }";
        var context = new CSharpSemanticContext(
            "Sample.cs",
            [new CSharpSourceDocument("Sample.cs", source)]);
        var position = source.IndexOf("System.Text", StringComparison.Ordinal);

        var actions = await service.GetActionsAsync(context, position);
        var action = actions.Single(candidate => candidate.DiagnosticId == "CS8019");
        var updated = source.Remove(action.Edit.StartOffset, action.Edit.Length)
            .Insert(action.Edit.StartOffset, action.Edit.NewText);

        Assert.Multiple(() =>
        {
            Assert.That(action.Title, Is.EqualTo("Remove unnecessary using"));
            Assert.That(action.Edit.NewText, Is.Empty);
            Assert.That(action.Edit.StartOffset, Is.EqualTo("using System;\n".Length));
            Assert.That(action.Edit.Length, Is.EqualTo("using System.Text;\n".Length));
            Assert.That(updated, Is.EqualTo("using System;\nnamespace Demo;\npublic sealed class Sample { public DateTime Value { get; } }"));
        });
    }

    [Test]
    public async Task DiagnosticOnAnotherLineDoesNotReturnAction()
    {
        var service = new RoslynCSharpCodeActionService();
        const string source = "namespace Demo;\npublic sealed class Sample\n{\n    public int Missing => 42\n    public int Good => 7;\n}";
        var context = new CSharpSemanticContext(
            "Sample.cs",
            [new CSharpSourceDocument("Sample.cs", source)]);
        var position = source.IndexOf("Good", StringComparison.Ordinal);

        var actions = await service.GetActionsAsync(context, position);

        Assert.That(actions, Is.Empty);
    }

    [Test]
    public async Task ValidCodeDoesNotReturnAction()
    {
        var service = new RoslynCSharpCodeActionService();
        const string source = "namespace Demo; public sealed class Sample { public int Value => 42; }";
        var context = new CSharpSemanticContext(
            "Sample.cs",
            [new CSharpSourceDocument("Sample.cs", source)]);

        var actions = await service.GetActionsAsync(context, source.IndexOf("Value", StringComparison.Ordinal));

        Assert.That(actions, Is.Empty);
    }
}
