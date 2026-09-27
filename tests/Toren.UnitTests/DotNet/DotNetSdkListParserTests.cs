using NUnit.Framework;
using Toren.DotNet.Environment.Parsing;

namespace Toren.UnitTests.DotNet;

[TestFixture]
public sealed class DotNetSdkListParserTests
{
    [Test]
    public void ParseExtractsStableAndPrereleaseSdks()
    {
        const string output = """
            8.0.419 [/usr/local/share/dotnet/sdk]
            10.0.100 [/usr/local/share/dotnet/sdk]
            11.0.100-preview.1 [/usr/local/share/dotnet/sdk]
            """;

        var result = DotNetSdkListParser.Parse(output);

        Assert.That(result, Has.Count.EqualTo(3));
        Assert.Multiple(() =>
        {
            Assert.That(result[0].Version, Is.EqualTo("8.0.419"));
            Assert.That(result[1].Version, Is.EqualTo("10.0.100"));
            Assert.That(result[2].IsPrerelease, Is.True);
        });
    }

    [Test]
    public void ParseIgnoresLinesThatDoNotMatchDotnetOutput()
    {
        const string output = """
            not-an-sdk
            10.0.100
            10.0.200 [/opt/dotnet/sdk]
            """;

        var result = DotNetSdkListParser.Parse(output);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Version, Is.EqualTo("10.0.200"));
    }
}
