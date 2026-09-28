using NUnit.Framework;
using Toren.Language.CSharp.Models;
using Toren.Language.CSharp.Services;

namespace Toren.UnitTests.Language;

[TestFixture]
public sealed class RoslynCSharpSymbolIndexServiceTests
{
    [Test]
    public async Task IndexesTypesAndMembersAcrossContextDocuments()
    {
        const string firstPath = "/repo/Customer.cs";
        const string secondPath = "/repo/CustomerService.cs";
        var context = new CSharpSemanticContext(
            secondPath,
            [
                new CSharpSourceDocument(
                    firstPath,
                    """
                    namespace Demo;
                    public sealed class Customer
                    {
                        private int _age;
                        public string Name { get; init; } = string.Empty;
                    }
                    """),
                new CSharpSourceDocument(
                    secondPath,
                    """
                    namespace Demo;
                    public sealed class CustomerService
                    {
                        public Customer Create() => new();
                    }
                    """),
            ]);
        var service = new RoslynCSharpSymbolIndexService();

        var symbols = await service.GetSymbolsAsync(context);

        Assert.Multiple(() =>
        {
            Assert.That(symbols.Any(symbol => symbol.Name == "Customer" && symbol.Kind == CSharpSymbolKind.Type), Is.True);
            Assert.That(symbols.Any(symbol => symbol.Name == "_age" && symbol.Kind == CSharpSymbolKind.Field), Is.True);
            Assert.That(symbols.Any(symbol => symbol.Name == "Name" && symbol.Kind == CSharpSymbolKind.Property), Is.True);
            Assert.That(symbols.Any(symbol => symbol.Name == "Create" && symbol.Kind == CSharpSymbolKind.Method), Is.True);
            Assert.That(symbols.Single(symbol => symbol.Name == "Create").Location.FilePath, Is.EqualTo(secondPath));
        });
    }

    [Test]
    public async Task IgnoresLocalVariablesInProjectSymbolIndex()
    {
        const string path = "/repo/Worker.cs";
        var context = new CSharpSemanticContext(
            path,
            [
                new CSharpSourceDocument(
                    path,
                    """
                    namespace Demo;
                    public sealed class Worker
                    {
                        public int Run()
                        {
                            var localValue = 42;
                            return localValue;
                        }
                    }
                    """),
            ]);
        var service = new RoslynCSharpSymbolIndexService();

        var symbols = await service.GetSymbolsAsync(context);

        Assert.That(symbols.Any(symbol => symbol.Name == "localValue"), Is.False);
    }
}
