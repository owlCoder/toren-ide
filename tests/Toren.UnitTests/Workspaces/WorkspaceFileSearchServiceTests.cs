using NUnit.Framework;
using Toren.Workspaces.Models;
using Toren.Workspaces.Services;

namespace Toren.UnitTests.Workspaces;

[TestFixture]
public sealed class WorkspaceFileSearchServiceTests
{
    private readonly WorkspaceFileSearchService _service = new();

    [Test]
    public void ExactFileNameRanksAheadOfPathAndFuzzyMatches()
    {
        WorkspaceFileEntry[] files =
        [
            new("/repo/src/ResultFactory.cs", "src/ResultFactory.cs", "ResultFactory.cs"),
            new("/repo/docs/result.cs.md", "docs/result.cs.md", "result.cs.md"),
            new("/repo/src/Result.cs", "src/Result.cs", "Result.cs"),
        ];

        var results = _service.Search(files, "Result.cs");

        Assert.Multiple(() =>
        {
            Assert.That(results, Has.Count.EqualTo(3));
            Assert.That(results[0].Name, Is.EqualTo("Result.cs"));
        });
    }

    [Test]
    public void FuzzySubsequenceMatchesFileName()
    {
        WorkspaceFileEntry[] files =
        [
            new("/repo/src/Result.cs", "src/Result.cs", "Result.cs"),
            new("/repo/src/Program.cs", "src/Program.cs", "Program.cs"),
        ];

        var results = _service.Search(files, "rslt");

        Assert.That(results.Select(result => result.Name), Is.EqualTo(new[] { "Result.cs" }));
    }

    [Test]
    public void EmptyQueryHonorsMaximumResultCount()
    {
        WorkspaceFileEntry[] files =
        [
            new("/repo/A.cs", "A.cs", "A.cs"),
            new("/repo/B.cs", "B.cs", "B.cs"),
            new("/repo/C.cs", "C.cs", "C.cs"),
        ];

        var results = _service.Search(files, string.Empty, maxResults: 2);

        Assert.That(results, Has.Count.EqualTo(2));
    }
}
