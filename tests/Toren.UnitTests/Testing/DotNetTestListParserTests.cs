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

    [Test]
    public void ReturnsEmptyWhenNoListHeaderExists()
    {
        var tests = DotNetTestListParser.Parse("No test is available in Sample.Tests.dll.");

        Assert.That(tests, Is.Empty);
    }
}
