using NUnit.Framework;
using Toren.Language.CSharp.Services;

namespace Toren.UnitTests.Language;

[TestFixture]
public sealed class RoslynCSharpFormattingServiceTests
{
    [Test]
    public async Task FormatNormalizesIndentationAndSpacing()
    {
        var service = new RoslynCSharpFormattingService();
        const string source = "namespace Demo;\npublic class Sample{public int Add(int a,int b){return a+b;}}";

        var formatted = await service.FormatAsync(source);

        Assert.Multiple(() =>
        {
            Assert.That(formatted, Does.Contain("public class Sample\n{"));
            Assert.That(formatted, Does.Contain("public int Add(int a, int b)"));
            Assert.That(formatted, Does.Contain("return a + b;"));
        });
    }

    [Test]
    public async Task FormatPreservesCrLfLineEndings()
    {
        var service = new RoslynCSharpFormattingService();
        const string source = "namespace Demo;\r\npublic class Sample{ }";

        var formatted = await service.FormatAsync(source);

        Assert.That(formatted, Does.Contain("\r\n"));
        Assert.That(formatted.Replace("\r\n", string.Empty, StringComparison.Ordinal), Does.Not.Contain("\n"));
    }
}
