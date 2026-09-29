using NUnit.Framework;
using Toren.Git.Parsers;

namespace Toren.UnitTests.Git;

[TestFixture]
public sealed class GitBranchParserTests
{
    [Test]
    public void ParseReadsCurrentBranchAndUpstream()
    {
        var output = string.Concat(
            "main\0*\0origin/main\0\n",
            "feature/git\0 \0origin/feature/git\0\n",
            "local-only\0 \0\0\n");

        var branches = GitBranchParser.Parse(output);

        Assert.Multiple(() =>
        {
            Assert.That(branches, Has.Count.EqualTo(3));
            Assert.That(branches[0].Name, Is.EqualTo("main"));
            Assert.That(branches[0].IsCurrent, Is.True);
            Assert.That(branches[0].UpstreamName, Is.EqualTo("origin/main"));
            Assert.That(branches[1].IsCurrent, Is.False);
            Assert.That(branches[2].UpstreamName, Is.Null);
        });
    }

    [Test]
    public void ParseIgnoresMalformedOrEmptyLines()
    {
        var branches = GitBranchParser.Parse("\ninvalid\nmain\0*\0\0\n");

        Assert.Multiple(() =>
        {
            Assert.That(branches, Has.Count.EqualTo(1));
            Assert.That(branches[0].Name, Is.EqualTo("main"));
        });
    }
}
