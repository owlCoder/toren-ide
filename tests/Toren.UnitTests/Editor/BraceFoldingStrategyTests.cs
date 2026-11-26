using AvaloniaEdit.Document;
using NUnit.Framework;
using Toren.App.Views.Editing;

namespace Toren.UnitTests.Editor;

[TestFixture]
public sealed class BraceFoldingStrategyTests
{
    private static string Lines(params string[] lines) => string.Join("\n", lines);

    private static List<AvaloniaEdit.Folding.NewFolding> Fold(string text) =>
        BraceFoldingStrategy.CreateFoldings(new TextDocument(text)).ToList();

    [Test]
    public void MultiLineBlockIsFoldedFromOpeningToClosingBrace()
    {
        var text = Lines("class A", "{", "    int x;", "}");

        var foldings = Fold(text);

        Assert.That(foldings, Has.Count.EqualTo(1));
        Assert.That(foldings[0].StartOffset, Is.EqualTo(text.IndexOf('{')));
        Assert.That(foldings[0].EndOffset, Is.EqualTo(text.LastIndexOf('}') + 1));
    }

    [Test]
    public void NestedBlocksAreMatchedWithAStack()
    {
        var text = Lines("class A", "{", "    void M()", "    {", "        var x = 1;", "    }", "}");

        var foldings = Fold(text);

        Assert.That(foldings, Has.Count.EqualTo(2));
        Assert.That(foldings[0].StartOffset, Is.EqualTo(text.IndexOf('{')));
        Assert.That(foldings[0].EndOffset, Is.EqualTo(text.LastIndexOf('}') + 1));
        Assert.That(foldings[1].StartOffset, Is.GreaterThan(foldings[0].StartOffset));
        Assert.That(foldings[1].EndOffset, Is.LessThan(foldings[0].EndOffset));
    }

    [Test]
    public void BlockOnASingleLineIsNotFolded()
    {
        Assert.That(Fold("var o = new { A = 1 };"), Is.Empty);
    }

    [Test]
    public void BracesInsideCommentsStringsAndCharLiteralsAreIgnored()
    {
        var text = Lines(
            "class A",
            "{",
            "    char c = '{';",
            "    string s = \"}\";",
            "    string v = @\"{ \"\" }\";",
            "    // }",
            "    /* } */",
            "}");

        var foldings = Fold(text);

        Assert.That(foldings, Has.Count.EqualTo(1));
        Assert.That(foldings[0].EndOffset, Is.EqualTo(text.LastIndexOf('}') + 1));
    }

    [Test]
    public void UnbalancedBracesDoNotThrowAndProduceNoFolding()
    {
        Assert.That(() => Fold("} { {"), Throws.Nothing);
        Assert.That(Fold("} { {"), Is.Empty);
    }

    [Test]
    public void JsonObjectIsFolded()
    {
        var foldings = Fold(Lines("{", "  \"name\": \"toren\"", "}"));

        Assert.That(foldings, Has.Count.EqualTo(1));
    }
}
