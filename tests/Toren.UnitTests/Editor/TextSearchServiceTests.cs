using NUnit.Framework;
using Toren.App.Editor.Services;

namespace Toren.UnitTests.Editor;

[TestFixture]
public sealed class TextSearchServiceTests
{
    private TextSearchService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new TextSearchService();
    }

    [Test]
    public void FindAllReturnsNonOverlappingMatches()
    {
        var matches = _service.FindAll("alpha beta alpha", "alpha", matchCase: true);

        Assert.Multiple(() =>
        {
            Assert.That(matches, Has.Count.EqualTo(2));
            Assert.That(matches[0].Offset, Is.EqualTo(0));
            Assert.That(matches[1].Offset, Is.EqualTo(11));
            Assert.That(matches.All(match => match.Length == 5), Is.True);
        });
    }

    [Test]
    public void FindAllCanIgnoreCase()
    {
        var matches = _service.FindAll("Result result RESULT", "result", matchCase: false);

        Assert.That(matches, Has.Count.EqualTo(3));
    }

    [Test]
    public void FindAllHonorsMatchCase()
    {
        var matches = _service.FindAll("Result result RESULT", "result", matchCase: true);

        Assert.That(matches, Has.Count.EqualTo(1));
        Assert.That(matches[0].Offset, Is.EqualTo(7));
    }

    [TestCase("")]
    [TestCase("missing")]
    public void FindAllReturnsEmptyWhenPatternDoesNotMatch(string pattern)
    {
        var matches = _service.FindAll("alpha beta", pattern, matchCase: false);

        Assert.That(matches, Is.Empty);
    }
}
