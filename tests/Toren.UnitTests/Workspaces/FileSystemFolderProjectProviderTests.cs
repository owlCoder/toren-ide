using NUnit.Framework;
using Toren.Workspaces.Adapters;

namespace Toren.UnitTests.Workspaces;

[TestFixture]
public sealed class FileSystemFolderProjectProviderTests
{
    [Test]
    public async Task DiscoversProjectsRecursivelyAndSkipsBuildOrIdeDirectories()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var appDirectory = Directory.CreateDirectory(Path.Combine(root, "src", "ParcelBox.Api")).FullName;
            var testDirectory = Directory.CreateDirectory(Path.Combine(root, "tests", "ParcelBox.Tests")).FullName;
            var binDirectory = Directory.CreateDirectory(Path.Combine(root, "src", "bin")).FullName;
            var objDirectory = Directory.CreateDirectory(Path.Combine(root, "obj")).FullName;
            var gitDirectory = Directory.CreateDirectory(Path.Combine(root, ".git")).FullName;
            var ideaDirectory = Directory.CreateDirectory(Path.Combine(root, ".idea")).FullName;

            var appProject = Path.Combine(appDirectory, "ParcelBox.Api.csproj");
            var testProject = Path.Combine(testDirectory, "ParcelBox.Tests.csproj");
            File.WriteAllText(appProject, "<Project />");
            File.WriteAllText(testProject, "<Project />");
            File.WriteAllText(Path.Combine(binDirectory, "Generated.csproj"), "<Project />");
            File.WriteAllText(Path.Combine(objDirectory, "Generated.csproj"), "<Project />");
            File.WriteAllText(Path.Combine(gitDirectory, "Hidden.csproj"), "<Project />");
            File.WriteAllText(Path.Combine(ideaDirectory, "Hidden.csproj"), "<Project />");

            var provider = new FileSystemFolderProjectProvider();
            var result = await provider.GetProjectPathsAsync(root);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Value, Has.Count.EqualTo(2));
                Assert.That(result.Value, Does.Contain(Path.GetFullPath(appProject)));
                Assert.That(result.Value, Does.Contain(Path.GetFullPath(testProject)));
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task ReportsUnavailableFolder()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"toren-missing-folder-{Guid.NewGuid():N}");
        var provider = new FileSystemFolderProjectProvider();

        var result = await provider.GetProjectPathsAsync(missing);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("workspace.folder.projects.unavailable"));
        });
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"toren-folder-projects-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
