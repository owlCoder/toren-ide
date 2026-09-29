using NUnit.Framework;
using Toren.DotNet.Testing.Services;

namespace Toren.UnitTests.Testing;

[TestFixture]
public sealed class DotNetTestListParserTests
{
    [Test]
    public void ParsesIndentedTestsAfterVSTestHeader()
    {
        const string output = """
            Test run for Sample.Tests.dll (.NETCoreApp,Version=v10.0)
            The following Tests are available:
                Sample.Tests.CalculatorTests.Adds_numbers
                Sample.Tests.CalculatorTests.Adds_numbers(1,2,3)
            Total tests: 2
            """;

        var tests = DotNetTestListParser.Parse(output);

        Assert.That(tests, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(tests[0].FullyQualifiedName, Is.EqualTo("Sample.Tests.CalculatorTests.Adds_numbers"));
            Assert.That(tests[0].DisplayName, Is.EqualTo("Adds_numbers"));
            Assert.That(tests[1].FullyQualifiedName, Is.EqualTo("Sample.Tests.CalculatorTests.Adds_numbers(1,2,3)"));
            Assert.That(tests[1].DisplayName, Is.EqualTo("Adds_numbers(1,2,3)"));
        });
    }

    [TestCase(
        "NUnit",
        "Sample.Tests.CalculatorTests.Adds_numbers")]
    [TestCase(
        "xUnit",
        "Sample.Tests.CalculatorTests.Adds_numbers(value: 42)")]
    [TestCase(
        "MSTest",
        "Sample.Tests.CalculatorTests.Adds_numbers (42)")]
    public void ParsesRepresentativeVSTestDiscoveryForSupportedFrameworks(
        string framework,
        string discoveredName)
    {
        var output = $"""
            Test run for {framework}.Tests.dll (.NETCoreApp,Version=v10.0)
            The following Tests are available:
                {discoveredName}
            Total tests: 1
            """;

        var tests = DotNetTestListParser.Parse(output);

        Assert.That(tests, Has.Count.EqualTo(1));
        Assert.That(tests[0].FullyQualifiedName, Is.EqualTo(discoveredName));
    }

    [Test]
    public void ReturnsEmptyWhenNoListHeaderExists()
    {
        var tests = DotNetTestListParser.Parse("No test is available in Sample.Tests.dll.");

        Assert.That(tests, Is.Empty);
    }
}
