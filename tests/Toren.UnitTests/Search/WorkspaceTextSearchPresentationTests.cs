using NUnit.Framework;
using Toren.App.Search.Models;

namespace Toren.UnitTests.Search;

[TestFixture]
public sealed class WorkspaceTextSearchPresentationTests
{
    [Test]
    public void BuildGroupsMatchesByFileAndKeepsResultOrderWithinEachFile()
    {
        var first = new WorkspaceTextSearchResult("/repo/src/A.cs", "src/A.cs", 3, 4, "first");
        var other = new WorkspaceTextSearchResult("/repo/src/B.cs", "src/B.cs", 7, 2, "other");
        var second = new WorkspaceTextSearchResult("/repo/src/A.cs", "src/A.cs", 9, 1, "second");

        var items = WorkspaceTextSearchPresentation.Build([first, other, second]);

        Assert.Multiple(() =>
        {
            Assert.That(items, Has.Count.EqualTo(5));
            Assert.That(items[0].IsHeader, Is.True);
            Assert.That(items[0].RelativePath, Is.EqualTo("src/A.cs"));
            Assert.That(items[0].MatchCount, Is.EqualTo(2));
            Assert.That(items[0].MatchCountText, Is.EqualTo("2 matches"));
            Assert.That(items[1].Result, Is.SameAs(first));
            Assert.That(items[2].Result, Is.SameAs(second));
            Assert.That(items[3].IsHeader, Is.True);
            Assert.That(items[3].RelativePath, Is.EqualTo("src/B.cs"));
            Assert.That(items[3].MatchCountText, Is.EqualTo("1 match"));
            Assert.That(items[4].Result, Is.SameAs(other));
        });
    }

    [Test]
    public void BuildReturnsEmptyPresentationForNoResults()
    {
        var items = WorkspaceTextSearchPresentation.Build([]);

        Assert.That(items, Is.Empty);
    }
}
