using NUnit.Framework;
using Toren.Language.CSharp.Models;
using Toren.Language.CSharp.Services;

namespace Toren.UnitTests.Language;

[TestFixture]
public sealed class RoslynCSharpSemanticServiceTests
{
    [Test]
    public async Task GetSymbolAsyncResolvesSameDocumentMethodDefinition()
    {
        const string source = """
            namespace Demo;
            public sealed class Calculator
            {
                private static int Add(int left, int right) => left + right;
                public int Sum() => Add(1, 2);
            }
            """;
        var service = new RoslynCSharpSemanticService();

        var symbol = await service.GetSymbolAsync(source, line: 5, column: 25);

        Assert.That(symbol, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(symbol!.Name, Is.EqualTo("Add"));
            Assert.That(symbol.Kind, Is.EqualTo(CSharpSymbolKind.Method));
            Assert.That(symbol.DisplayText, Does.Contain("Add"));
            Assert.That(symbol.Definition, Is.Not.Null);
            Assert.That(symbol.Definition!.Line, Is.EqualTo(4));
            Assert.That(symbol.Definition.Column, Is.GreaterThan(0));
        });
    }

    [Test]
    public async Task GetSymbolAsyncResolvesFrameworkTypeWithoutSourceDefinition()
    {
        const string source = """
            namespace Demo;
            public sealed class Person
            {
                public string Name { get; init; } = string.Empty;
            }
            """;
        var service = new RoslynCSharpSemanticService();

        var symbol = await service.GetSymbolAsync(source, line: 4, column: 12);

        Assert.That(symbol, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(symbol!.Name, Is.EqualTo("String"));
            Assert.That(symbol.Kind, Is.EqualTo(CSharpSymbolKind.Type));
            Assert.That(symbol.Definition, Is.Null);
        });
    }

    [TestCase(0, 1)]
    [TestCase(1, 0)]
    [TestCase(20, 1)]
    public async Task GetSymbolAsyncReturnsNullForInvalidLocation(int line, int column)
    {
        var service = new RoslynCSharpSemanticService();

        var symbol = await service.GetSymbolAsync("class Example { }", line, column);

        Assert.That(symbol, Is.Null);
    }
}
