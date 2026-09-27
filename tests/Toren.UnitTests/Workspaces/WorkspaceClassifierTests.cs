using NUnit.Framework;
using Toren.Workspaces;

namespace Toren.UnitTests.Workspaces;

[TestFixture]
public sealed class WorkspaceClassifierTests
{
    private readonly WorkspaceClassifier _classifier = new();

    [Test]
    public void ClassifyDirectory_returns_folder_workspace()
    {
        var path = Path.Combine(Path.GetTempPath(), "toren-sample");

        var result = _classifier.ClassifyDirectory(path);

        Assert.Multiple(() =>
        {
            Assert.That(result.Kind, Is.EqualTo(WorkspaceKind.Folder));
            Assert.That(result.DisplayName, Is.EqualTo("toren-sample"));
            Assert.That(result.Path, Is.EqualTo(Path.GetFullPath(path)));
        });
    }

    [TestCase("Sample.sln", WorkspaceKind.Solution)]
    [TestCase("Sample.slnx", WorkspaceKind.SolutionX)]
    [TestCase("Sample.csproj", WorkspaceKind.Project)]
    public void TryClassifyFile_recognizes_dotnet_workspace_files(string fileName, WorkspaceKind expectedKind)
    {
        var path = Path.Combine(Path.GetTempPath(), fileName);

        var success = _classifier.TryClassifyFile(path, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(success, Is.True);
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Kind, Is.EqualTo(expectedKind));
            Assert.That(result.DisplayName, Is.EqualTo("Sample"));
        });
    }

    [Test]
    public void TryClassifyFile_rejects_unsupported_files()
    {
        var path = Path.Combine(Path.GetTempPath(), "notes.txt");

        var success = _classifier.TryClassifyFile(path, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(success, Is.False);
            Assert.That(result, Is.Null);
        });
    }
}
