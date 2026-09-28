using NUnit.Framework;
using Toren.Language.CSharp.Models;
using Toren.Language.CSharp.Services;

namespace Toren.UnitTests.Language;

[TestFixture]
public sealed class RoslynCSharpRenameServiceTests
{
    [Test]
    public async Task RenameUpdatesDeclarationAndCrossFileReferences()
    {
        var service = new RoslynCSharpRenameService();
        var context = new CSharpSemanticContext(
            "Service.cs",
            [
                new CSharpSourceDocument(
                    "Service.cs",
                    "namespace Demo; public sealed class Service { public int Value => 42; }"),
                new CSharpSourceDocument(
                    "Consumer.cs",
                    "namespace Demo; public sealed class Consumer { public int Get(Service service) => service.Value; }"),
            ]);

        var result = await service.RenameAsync(context, 1, 58, "Answer");

        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result!.OriginalName, Is.EqualTo("Value"));
            Assert.That(result.NewName, Is.EqualTo("Answer"));
            Assert.That(result.Documents, Has.Count.EqualTo(2));
            Assert.That(
                result.Documents.Single(document => document.Path == "Service.cs").Text,
                Does.Contain("Answer => 42"));
            Assert.That(
                result.Documents.Single(document => document.Path == "Consumer.cs").Text,
                Does.Contain("service.Answer"));
        });
    }

    [Test]
    public async Task RenameDoesNotChangeUnrelatedSymbolWithSameName()
    {
        var service = new RoslynCSharpRenameService();
        var context = new CSharpSemanticContext(
            "Sample.cs",
            [
                new CSharpSourceDocument(
                    "Sample.cs",
                    "namespace Demo; public sealed class Sample { public int Get(int value) { var other = value; return other; } public int Other(int other) => other; }"),
            ]);

        var result = await service.RenameAsync(context, 1, 79, "input");

        Assert.That(result, Is.Not.Null);
        var text = result!.Documents.Single().Text;
        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("Get(int input)"));
            Assert.That(text, Does.Contain("var other = input"));
            Assert.That(text, Does.Contain("Other(int other) => other"));
        });
    }

    [Test]
    public async Task RenameRejectsInvalidIdentifier()
    {
        var service = new RoslynCSharpRenameService();
        var context = new CSharpSemanticContext(
            "Sample.cs",
            [new CSharpSourceDocument("Sample.cs", "namespace Demo; public sealed class Sample { }")]);

        var result = await service.RenameAsync(context, 1, 44, "not valid");

        Assert.That(result, Is.Null);
    }
}
