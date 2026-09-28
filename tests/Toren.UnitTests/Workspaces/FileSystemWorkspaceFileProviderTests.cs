using NUnit.Framework;
using Toren.Workspaces.Adapters;

namespace Toren.UnitTests.Workspaces;

[TestFixture]
public sealed class FileSystemWorkspaceFileProviderTests
{
    [Test]
    public async Task DiscoversWorkspaceFilesAndSkipsGeneratedDirectories()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var sourceDirectory = Directory.CreateDirectory(Path.Combine(root, "src", "App")).FullName;
            var binDirectory = Directory.CreateDirectory(Path.Combine(root, "src", "App", "bin")).FullName;
            var objDirectory = Directory.CreateDirectory(Path.Combine(root, "obj")).FullName;
            var gitDirectory = Directory.CreateDirectory(Path.Combine(root, ".git")).FullName;
            var sourceFile = Path.Combine(sourceDirectory, "Program.cs");
            var readmeFile = Path.Combine(root, "README.md");
            File.WriteAllText(sourceFile, "class Program { }");
            File.WriteAllText(readmeFile, "# Test");
            File.WriteAllText(Path.Combine(binDirectory, "Generated.cs"), "generated");
            File.WriteAllText(Path.Combine(objDirectory, "Generated.cs"), "generated");
            File.WriteAllText(Path.Combine(gitDirectory, "config"), "hidden");

            var provider = new FileSystemWorkspaceFileProvider();
            var result = await provider.GetFilesAsync(root);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Value.Select(file => file.Path), Does.Contain(Path.GetFullPath(sourceFile)));
                Assert.That(result.Value.Select(file => file.Path), Does.Contain(Path.GetFullPath(readmeFile)));
                Assert.That(result.Value.Any(file => file.RelativePath.Contains("bin", StringComparison.OrdinalIgnoreCase)), Is.False);
                Assert.That(result.Value.Any(file => file.RelativePath.Contains("obj", StringComparison.OrdinalIgnoreCase)), Is.False);
                Assert.That(result.Value.Any(file => file.RelativePath.Contains(".git", StringComparison.OrdinalIgnoreCase)), Is.False);
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task UsesParentDirectoryWhenWorkspacePathIsAFile()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var solutionPath = Path.Combine(root, "Sample.sln");
            var sourcePath = Path.Combine(root, "Program.cs");
            File.WriteAllText(solutionPath, string.Empty);
            File.WriteAllText(sourcePath, "class Program { }");

            var provider = new FileSystemWorkspaceFileProvider();
            var result = await provider.GetFilesAsync(solutionPath);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Value.Select(file => file.Name), Does.Contain("Sample.sln"));
                Assert.That(result.Value.Select(file => file.Name), Does.Contain("Program.cs"));
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"toren-workspace-files-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
