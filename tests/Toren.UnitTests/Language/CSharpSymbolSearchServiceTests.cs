using NUnit.Framework;
using Toren.Language.CSharp.Models;
using Toren.Language.CSharp.Services;

namespace Toren.UnitTests.Language;

[TestFixture]
public sealed class CSharpSymbolSearchServiceTests
{
    private readonly CSharpSymbolSearchService _service = new();

    [Test]
    public void ExactNameRanksAheadOfPrefixAndContainerMatches()
    {
        CSharpWorkspaceSymbol[] symbols =
        [
            Create("CreateCustomer", "CustomerService"),
            Create("Customer", "Domain"),
            Create("Save", "Customer"),
        ];

        var results = _service.Search(symbols, "Customer");

        Assert.Multiple(() =>
        {
            Assert.That(results, Has.Count.EqualTo(3));
            Assert.That(results[0].Name, Is.EqualTo("Customer"));
        });
    }

    [Test]
    public void FuzzyNameSearchFindsSubsequence()
    {
        CSharpWorkspaceSymbol[] symbols =
        [
            Create("CustomerService", "Services"),
            Create("OrderService", "Services"),
        ];

        var results = _service.Search(symbols, "ctmsv");

        Assert.Multiple(() =>
        {
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].Name, Is.EqualTo("CustomerService"));
        });
    }

    private static CSharpWorkspaceSymbol Create(string name, string container) =>
        new(
            name,
            name,
            CSharpSymbolKind.Type,
            container,
            new CSharpSourceLocation("/repo/Test.cs", 1, 1));
}
