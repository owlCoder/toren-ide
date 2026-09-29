using NUnit.Framework;
using Toren.DotNet.Testing.Parsers;

namespace Toren.UnitTests.Testing;

[TestFixture]
public sealed class DotNetTestOutputLocationParserTests
{
    [Test]
    public void ParsesUnixStackTraceLocation()
    {
        const string line = "   at Sample.Tests.CalculatorTests.Adds() in /repo/tests/CalculatorTests.cs:line 42";

        var location = DotNetTestOutputLocationParser.Parse(line);

        Assert.That(location, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(location!.FilePath, Is.EqualTo("/repo/tests/CalculatorTests.cs"));
            Assert.That(location.Line, Is.EqualTo(42));
            Assert.That(location.Column, Is.EqualTo(1));
        });
    }

    [Test]
    public void ParsesWindowsStackTraceLocationWithSpaces()
    {
        const string line = @"   at Sample.Tests.CalculatorTests.Adds() in C:\work dir\tests\Calculator Tests.cs:line 17";

        var location = DotNetTestOutputLocationParser.Parse(line);

        Assert.That(location, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(location!.FilePath, Is.EqualTo(@"C:\work dir\tests\Calculator Tests.cs"));
            Assert.That(location.Line, Is.EqualTo(17));
        });
    }

    [TestCase("Expected: 42")]
    [TestCase("   at Sample.Tests.CalculatorTests.Adds()")]
    [TestCase("   at Sample.Tests.CalculatorTests.Adds() in /repo/tests/readme.txt:line 42")]
    public void IgnoresNonNavigableOutput(string line)
    {
        Assert.That(DotNetTestOutputLocationParser.Parse(line), Is.Null);
    }
}
