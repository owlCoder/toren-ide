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

            Assert.That(result.IsSuccess, Is.True);
            var files = result.Value!;
            Assert.Multiple(() =>
            {
                Assert.That(files.Select(file => file.Path), Does.Contain(Path.GetFullPath(sourceFile)));
                Assert.That(files.Select(file => file.Path), Does.Contain(Path.GetFullPath(readmeFile)));
                Assert.That(files.Any(file => file.RelativePath.Contains("bin", StringComparison.OrdinalIgnoreCase)), Is.False);
                Assert.That(files.Any(file => file.RelativePath.Contains("obj", StringComparison.OrdinalIgnoreCase)), Is.False);
                Assert.That(files.Any(file => file.RelativePath.Contains(".git", StringComparison.OrdinalIgnoreCase)), Is.False);
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

            Assert.That(result.IsSuccess, Is.True);
            var files = result.Value!;
            Assert.Multiple(() =>
            {
                Assert.That(files.Select(file => file.Name), Does.Contain("Sample.sln"));
                Assert.That(files.Select(file => file.Name), Does.Contain("Program.cs"));
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task MatchesAPlainRecursiveListingIncludingHiddenFilesAndOrder()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            foreach (var file in new[]
                     {
                         "b.txt", "A.txt", ".hidden", "src/App/Program.cs", "src/App/Nested/Deep/File.cs",
                         "src/app2/Lower.cs", "src/App/obj/Generated.cs", "src/App/Bin/Output.dll",
                         "node_modules/package/index.js", ".vs/state", ".idea/workspace.xml", "docs/read me.md",
                     })
            {
                var path = Path.Combine(root, file.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, string.Empty);
            }

            Directory.CreateDirectory(Path.Combine(root, "src", "Empty"));
            try
            {
                Directory.CreateSymbolicLink(Path.Combine(root, "linked-directory"), Path.Combine(root, "src"));
                File.CreateSymbolicLink(Path.Combine(root, "linked-file.cs"), Path.Combine(root, "src", "App", "Program.cs"));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Creating links needs a privilege on Windows; the rest of the comparison still applies.
            }

            var result = await new FileSystemWorkspaceFileProvider().GetFilesAsync(root);

            Assert.That(result.IsSuccess, Is.True);
            var expected = ListRecursively(Path.GetFullPath(root));
            Assert.Multiple(() =>
            {
                Assert.That(result.Value!.Select(file => file.Path), Is.EqualTo(expected.Select(file => file.Path)));
                Assert.That(result.Value!.Select(file => file.RelativePath), Is.EqualTo(expected.Select(file => file.RelativePath)));
                Assert.That(result.Value!.Select(file => file.Name), Is.EqualTo(expected.Select(file => file.Name)));
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>The straightforward listing the provider must agree with.</summary>
    private static List<(string Path, string RelativePath, string Name)> ListRecursively(string root)
    {
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".git", ".idea", ".vs", "bin", "node_modules", "obj" };
        var files = new List<(string Path, string RelativePath, string Name)>();
        var pending = new Stack<string>([root]);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                files.Add((
                    Path.GetFullPath(file),
                    Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/'),
                    Path.GetFileName(file)));
            }

            foreach (var child in Directory.EnumerateDirectories(directory))
            {
                if (!excluded.Contains(Path.GetFileName(child))
                    && (File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0)
                {
                    pending.Push(child);
                }
            }
        }

        return files
            .OrderBy(static file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static file => file.Path, StringComparer.Ordinal)
            .ToList();
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"toren-workspace-files-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
