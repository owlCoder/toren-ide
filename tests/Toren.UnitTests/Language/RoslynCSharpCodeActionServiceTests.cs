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
