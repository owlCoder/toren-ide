using NUnit.Framework;
using Toren.App.ViewModels;
using Toren.App.ViewModels.Utilities;

namespace Toren.UnitTests.Editor;

[TestFixture]
public sealed class BreadcrumbBuilderTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "toren-tests", "ws");

    [Test]
    public void DocumentInsideWorkspaceProducesRootFoldersAndFile()
    {
        var document = Path.Combine(Root, "src", "Services", "Parser.cs");

        var segments = BreadcrumbBuilder.Build(document, Root);

        Assert.That(segments.Select(segment => segment.Name), Is.EqualTo(new[] { "ws", "src", "Services", "Parser.cs" }));
        Assert.That(segments[0].Path, Is.EqualTo(Root));
        Assert.That(segments[^1].Path, Is.EqualTo(document));
    }

    [Test]
    public void OnlyTheLastSegmentHasNoNext()
    {
        var segments = BreadcrumbBuilder.Build(Path.Combine(Root, "a", "b.cs"), Root);

        Assert.That(segments.Take(segments.Count - 1).All(segment => segment.HasNext), Is.True);
        Assert.That(segments[^1].HasNext, Is.False);
    }

    [Test]
    public void DocumentOutsideWorkspaceShowsParentFolderAndFile()
    {
        var document = Path.Combine(Path.GetTempPath(), "toren-tests", "other", "Notes.md");

        var segments = BreadcrumbBuilder.Build(document, Root);

        Assert.That(segments.Select(segment => segment.Name), Is.EqualTo(new[] { "other", "Notes.md" }));
    }

    [Test]
    public void WithoutWorkspaceRootShowsParentFolderAndFile()
    {
        var document = Path.Combine(Root, "src", "Program.cs");

        var segments = BreadcrumbBuilder.Build(document, null);

        Assert.That(segments.Select(segment => segment.Name), Is.EqualTo(new[] { "src", "Program.cs" }));
    }

    [Test]
    public void SiblingFolderWithSamePrefixIsNotInsideWorkspace()
    {
        var document = Path.Combine(Path.GetTempPath(), "toren-tests", "ws-extra", "File.cs");

        var segments = BreadcrumbBuilder.Build(document, Root);

        Assert.That(segments.Select(segment => segment.Name), Is.EqualTo(new[] { "ws-extra", "File.cs" }));
    }
}
