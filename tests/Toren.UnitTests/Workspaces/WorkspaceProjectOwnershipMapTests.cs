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

    [Test]
    public void FirstProjectWinsWhenProjectsShareADirectoryOrALinkedFile()
    {
        var directory = Path.GetFullPath("/repo/App");
        var linkedFile = Path.GetFullPath("/repo/Shared/Linked.cs");
        var first = CreateProject(Path.Combine(directory, "First.csproj"), "First");
        first = first with { Metadata = first.Metadata with { SourcePaths = [linkedFile] } };
        var second = CreateProject(Path.Combine(directory, "Second.csproj"), "Second");
        second = second with { Metadata = second.Metadata with { SourcePaths = [linkedFile, Path.Combine(directory, "Only.cs")] } };
        var ownership = new WorkspaceProjectOwnershipMap([first, second]);

        Assert.Multiple(() =>
        {
            Assert.That(ownership.FindOwningProject(Path.Combine(directory, "Nested", "Deep", "File.cs")), Is.SameAs(first));
            Assert.That(ownership.FindOwningProject(linkedFile), Is.SameAs(first));
            Assert.That(ownership.FindOwningProject(Path.Combine(directory, "Only.cs")), Is.SameAs(second));
            Assert.That(ownership.FindOwningProject(Path.GetFullPath("/repo/Shared/Other.cs")), Is.Null);
        });
    }

    [Test]
    public void ListsSourceReportsEvaluatedCompileItemsPerProject()
    {
        var linkedFile = Path.GetFullPath("/repo/Shared/Linked.cs");
        var first = CreateProject(Path.GetFullPath("/repo/First/First.csproj"), "First");
        first = first with { Metadata = first.Metadata with { SourcePaths = [linkedFile] } };
        var second = CreateProject(Path.GetFullPath("/repo/Second/Second.csproj"), "Second");
        second = second with { Metadata = second.Metadata with { SourcePaths = ["/repo/Shared/../Shared/Linked.cs"] } };
        var third = CreateProject(Path.GetFullPath("/repo/Third/Third.csproj"), "Third");
        var ownership = new WorkspaceProjectOwnershipMap([first, second]);

        Assert.Multiple(() =>
        {
            Assert.That(ownership.ListsSource(first, linkedFile), Is.True);
            Assert.That(ownership.ListsSource(second, linkedFile), Is.True);
            Assert.That(ownership.ListsSource(first, Path.GetFullPath("/repo/First/Program.cs")), Is.False);
            Assert.That(ownership.ListsSource(third, linkedFile), Is.False);
        });
    }

    [Test]
    public void LookupsStayFastForLargeWorkspaces()
    {
        var projects = Enumerable.Range(0, 300)
            .Select(index =>
            {
                var directory = Path.GetFullPath($"/repo/src/Project{index}");
                var project = CreateProject(Path.Combine(directory, $"Project{index}.csproj"), $"Project{index}");
                return project with
                {
                    Metadata = project.Metadata with
                    {
                        SourcePaths = Enumerable.Range(0, 30).Select(file => Path.Combine(directory, $"File{file}.cs")).ToArray(),
                    },
                };
            })
            .ToArray();
        var ownership = new WorkspaceProjectOwnershipMap(projects);
        var watch = System.Diagnostics.Stopwatch.StartNew();

        // 9,000 unlisted files: the previous linear scan compared each against all 9,000 sources.
        for (var index = 0; index < 300; index++)
        {
            for (var file = 0; file < 30; file++)
            {
                var path = Path.GetFullPath($"/repo/src/Project{index}/Nested/Unlisted{file}.txt");
                Assert.That(ownership.FindOwningProject(path), Is.SameAs(projects[index]));
            }
        }

        Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)));
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
