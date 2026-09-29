using NUnit.Framework;
using Toren.Git.Parsers;

namespace Toren.UnitTests.Git;

[TestFixture]
public sealed class GitStatusParserTests
{
    [Test]
    public void ParseReadsBranchAheadBehindTrackedRenameAndUntrackedChanges()
    {
        var output = string.Join(
            '\0',
            "# branch.head main",
            "# branch.upstream origin/main",
            "# branch.ab +2 -1",
            "1 M. N... 100644 100644 100644 1111111 2222222 src/Program.cs",
            "1 .M N... 100644 100644 100644 3333333 4444444 README.md",
            "2 R. N... 100644 100644 100644 5555555 6666666 R100 src/New Name.cs",
            "src/Old Name.cs",
            "? notes/todo.txt",
            string.Empty);

        var status = GitStatusParser.Parse(output);

        Assert.Multiple(() =>
        {
            Assert.That(status.BranchName, Is.EqualTo("main"));
            Assert.That(status.UpstreamName, Is.EqualTo("origin/main"));
            Assert.That(status.AheadCount, Is.EqualTo(2));
            Assert.That(status.BehindCount, Is.EqualTo(1));
            Assert.That(status.Changes, Has.Count.EqualTo(4));
            Assert.That(status.Changes[0].IsStaged, Is.True);
            Assert.That(status.Changes[1].HasWorkingTreeChange, Is.True);
            Assert.That(status.Changes[2].Path, Is.EqualTo("src/New Name.cs"));
            Assert.That(status.Changes[2].OriginalPath, Is.EqualTo("src/Old Name.cs"));
            Assert.That(status.Changes[3].IsUntracked, Is.True);
        });
    }

    [Test]
    public void ParseReadsUnmergedConflictRecord()
    {
        var output = string.Join(
            '\0',
            "# branch.head main",
            "u UU N... 100644 100644 100644 100644 1111111 2222222 3333333 src/Conflict.cs",
            string.Empty);

        var status = GitStatusParser.Parse(output);

        Assert.Multiple(() =>
        {
            Assert.That(status.Changes, Has.Count.EqualTo(1));
            Assert.That(status.Changes[0].Path, Is.EqualTo("src/Conflict.cs"));
            Assert.That(status.Changes[0].IndexStatus, Is.EqualTo('U'));
            Assert.That(status.Changes[0].WorkTreeStatus, Is.EqualTo('U'));
            Assert.That(status.Changes[0].IsConflicted, Is.True);
            Assert.That(status.Changes[0].IsStaged, Is.False);
            Assert.That(status.Changes[0].HasWorkingTreeChange, Is.True);
        });
    }

    [Test]
    public void ParseTreatsDetachedHeadAsNoBranchName()
    {
        var status = GitStatusParser.Parse("# branch.head (detached)\0");

        Assert.That(status.BranchName, Is.Null);
    }
}
