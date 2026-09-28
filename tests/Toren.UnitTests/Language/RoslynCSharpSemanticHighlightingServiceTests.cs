using NUnit.Framework;
using Toren.Language.CSharp.Models;
using Toren.Language.CSharp.Services;

namespace Toren.UnitTests.Language;

[TestFixture]
public sealed class RoslynCSharpSemanticHighlightingServiceTests
{
    [Test]
    public async Task HighlightsDeclaredAndReferencedSymbolsBySemanticKind()
    {
        const string source = """
            namespace Demo;

            sealed class Widget
            {
                private int _count;

                int Run(int input)
                {
                    var current = _count + input;
                    return current;
                }
            }
            """;
        var context = new CSharpSemanticContext(
            "test.cs",
            [new CSharpSourceDocument("test.cs", source)]);
        var service = new RoslynCSharpSemanticHighlightingService();

        var highlights = await service.GetHighlightsAsync(context);

        Assert.Multiple(() =>
        {
            AssertHighlight(highlights, source, "Widget", CSharpSymbolKind.Type);
            AssertHighlight(highlights, source, "Run", CSharpSymbolKind.Method);
            AssertHighlight(highlights, source, "_count", CSharpSymbolKind.Field);
            AssertHighlight(highlights, source, "input", CSharpSymbolKind.Parameter);
            AssertHighlight(highlights, source, "current", CSharpSymbolKind.Local);
        });
    }

    [Test]
    public async Task DoesNotTreatIdentifiersInsideStringsOrCommentsAsSemanticSymbols()
    {
        const string source = """
            sealed class Widget
            {
                string Text => "Widget"; // Widget is just text here.
            }
            """;
        var context = new CSharpSemanticContext(
            "test.cs",
            [new CSharpSourceDocument("test.cs", source)]);
        var service = new RoslynCSharpSemanticHighlightingService();

        var highlights = await service.GetHighlightsAsync(context);

        var declarationOffset = source.IndexOf("Widget", StringComparison.Ordinal);
        var stringOffset = source.IndexOf("Widget", declarationOffset + 1, StringComparison.Ordinal);
        var commentOffset = source.IndexOf("Widget", stringOffset + 1, StringComparison.Ordinal);
        Assert.Multiple(() =>
        {
            Assert.That(highlights.Any(item => item.StartOffset == declarationOffset), Is.True);
            Assert.That(highlights.Any(item => item.StartOffset == stringOffset), Is.False);
            Assert.That(highlights.Any(item => item.StartOffset == commentOffset), Is.False);
        });
    }

    private static void AssertHighlight(
        IReadOnlyList<CSharpSemanticHighlight> highlights,
        string source,
        string identifier,
        CSharpSymbolKind kind)
    {
        var offset = source.IndexOf(identifier, StringComparison.Ordinal);
        Assert.That(offset, Is.GreaterThanOrEqualTo(0));
        Assert.That(
            highlights.Any(item =>
                item.StartOffset == offset
                && item.Length == identifier.Length
                && item.Kind == kind),
            Is.True,
            $"Expected semantic highlight for {identifier} as {kind}.");
    }
}
