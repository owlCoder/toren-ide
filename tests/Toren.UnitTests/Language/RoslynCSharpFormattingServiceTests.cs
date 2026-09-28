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
    public async Task FormatSelectionOnlyChangesSelectedMember()
    {
        var service = new RoslynCSharpFormattingService();
        const string source = "namespace Demo;\npublic class Sample\n{\n    public int Add(int a,int b){return a+b;}\n    public int Untouched(int a,int b){return a+b;}\n}";
        var selectionStart = source.IndexOf("public int Add", StringComparison.Ordinal);
        var selectionLength = source.IndexOf("    public int Untouched", StringComparison.Ordinal) - selectionStart;

        var formatted = await service.FormatSelectionAsync(source, selectionStart, selectionLength);

        Assert.Multiple(() =>
        {
            Assert.That(formatted, Does.Contain("public int Add(int a, int b)"));
            Assert.That(formatted, Does.Contain("return a + b;"));
            Assert.That(formatted, Does.Contain("public int Untouched(int a,int b){return a+b;}"));
        });
    }

    [Test]
    public async Task FormatSelectionWithEmptySpanPreservesSource()
    {
        var service = new RoslynCSharpFormattingService();
        const string source = "public class Sample{ }";

        var formatted = await service.FormatSelectionAsync(source, 0, 0);

        Assert.That(formatted, Is.EqualTo(source));
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
