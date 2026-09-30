using NUnit.Framework;
using Toren.Workspaces.Models;
using Toren.Workspaces.Services;

namespace Toren.UnitTests.Workspaces;

[TestFixture]
public sealed class WorkspaceProjectOwnershipMapTests
{
    [Test]
    public void FindOwningProjectUsesDeepestContainingProjectDirectory()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var outerDirectory = Path.Combine(root, "Outer");
            var nestedDirectory = Path.Combine(outerDirectory, "Nested");
            Directory.CreateDirectory(nestedDirectory);
            var outer = CreateProject(Path.Combine(outerDirectory, "Outer.csproj"), "Outer");
            var nested = CreateProject(Path.Combine(nestedDirectory, "Nested.csproj"), "Nested");
            var ownership = new WorkspaceProjectOwnershipMap([outer, nested]);

            var outerOwner = ownership.FindOwningProject(Path.Combine(outerDirectory, "Outer.cs"));
            var nestedOwner = ownership.FindOwningProject(Path.Combine(nestedDirectory, "Inner.cs"));
            var outsideOwner = ownership.FindOwningProject(Path.Combine(root, "Loose.cs"));

            Assert.Multiple(() =>
            {
                Assert.That(outerOwner, Is.SameAs(outer));
                Assert.That(nestedOwner, Is.SameAs(nested));
                Assert.That(outsideOwner, Is.Null);
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void EvaluatedCompileItemsOwnLinkedFilesOutsideTheProjectDirectory()
    {
        var linkedFile = Path.GetFullPath("/repo/Shared/Linked.cs");
        var project = CreateProject(Path.GetFullPath("/repo/App/App.csproj"), "App");
        project = project with { Metadata = project.Metadata with { SourcePaths = [linkedFile] } };
        Assert.That(new WorkspaceProjectOwnershipMap([project]).FindOwningProject(linkedFile), Is.SameAs(project));
    }

    private static WorkspaceProject CreateProject(string path, string displayName) =>
        new(
            path,
            displayName,
            new ProjectMetadata([], null, null, null, false, false, null, null, null),
            []);

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"toren-ownership-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
