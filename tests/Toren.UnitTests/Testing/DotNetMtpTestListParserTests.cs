using NUnit.Framework;
using Toren.DotNet.Testing.Parsers;

namespace Toren.UnitTests.Testing;

[TestFixture]
public sealed class DotNetMtpTestListParserTests
{
    [Test]
    public void ParsesStructuredDiscoveryIdentityAndMethodName()
    {
        const string output = """
            Build output before discovery
            {
              "schemaVersion": 1,
              "tests": [
                {
                  "uid": "uid-123",
                  "displayName": "Adds(1,2)",
                  "type": {
                    "assemblyFullName": "Sample.Tests, Version=1.0.0.0",
                    "namespace": "Sample.Tests",
                    "typeName": "CalculatorTests",
                    "methodName": "Adds",
                    "methodArity": 0,
                    "returnTypeFullName": "System.Void",
                    "parameterTypeFullNames": []
                  }
                }
              ]
            }
            """;

        var tests = DotNetMtpTestListParser.Parse(output);

        Assert.That(tests, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(tests[0].RunnerId, Is.EqualTo("uid-123"));
            Assert.That(tests[0].DisplayName, Is.EqualTo("Adds(1,2)"));
            Assert.That(tests[0].FullyQualifiedName, Is.EqualTo("Sample.Tests.CalculatorTests.Adds"));
        });
    }

    [Test]
    public void FallsBackToDisplayNameWhenMethodMetadataIsUnavailable()
    {
        const string output = """
            {
              "schemaVersion": 1,
              "tests": [
                {
                  "uid": "uid-standalone",
                  "displayName": "Standalone test"
                }
              ]
            }
            """;

        var tests = DotNetMtpTestListParser.Parse(output);

        Assert.Multiple(() =>
        {
            Assert.That(tests[0].FullyQualifiedName, Is.EqualTo("Standalone test"));
            Assert.That(tests[0].RunnerId, Is.EqualTo("uid-standalone"));
        });
    }

    [Test]
    public void RejectsUnsupportedSchema()
    {
        const string output = """
            {
              "schemaVersion": 2,
              "tests": []
            }
            """;

        Assert.That(
            () => DotNetMtpTestListParser.Parse(output),
            Throws.TypeOf<System.Text.Json.JsonException>());
    }
}
