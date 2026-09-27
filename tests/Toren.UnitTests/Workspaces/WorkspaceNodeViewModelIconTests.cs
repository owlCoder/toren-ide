using NUnit.Framework;
using Toren.App.ViewModels;
using Toren.Workspaces.Models;

namespace Toren.UnitTests.Workspaces;

[TestFixture]
public sealed class WorkspaceNodeViewModelIconTests
{
    [TestCase(WorkspaceNodeKind.Folder, "src", ExplorerIconKind.Folder)]
    [TestCase(WorkspaceNodeKind.Solution, "Toren.slnx", ExplorerIconKind.Solution)]
    [TestCase(WorkspaceNodeKind.References, "Dependencies", ExplorerIconKind.References)]
    [TestCase(WorkspaceNodeKind.Reference, "Avalonia", ExplorerIconKind.Reference)]
    [TestCase(WorkspaceNodeKind.SymbolicLink, "linked", ExplorerIconKind.Reference)]
    public void NodeKindsUseExpectedIcons(WorkspaceNodeKind kind, string name, ExplorerIconKind expectedIcon)
    {
        var viewModel = CreateNode(kind, name);

        Assert.That(viewModel.IconKind, Is.EqualTo(expectedIcon));
    }

    [TestCase("Toren.App.csproj", ExplorerIconKind.Project)]
    [TestCase("Toren.Tests.csproj", ExplorerIconKind.TestProject)]
    [TestCase("Toren.UnitTests.csproj", ExplorerIconKind.TestProject)]
    [TestCase("Toren.IntegrationTests.csproj", ExplorerIconKind.TestProject)]
    [TestCase("Tests.csproj", ExplorerIconKind.TestProject)]
    [TestCase("Contest.csproj", ExplorerIconKind.Project)]
    public void ProjectNamesUseExpectedIcons(string name, ExplorerIconKind expectedIcon)
    {
        var viewModel = CreateNode(WorkspaceNodeKind.Project, name);

        Assert.That(viewModel.IconKind, Is.EqualTo(expectedIcon));
    }

    [TestCase("Program.cs", ExplorerIconKind.Code)]
    [TestCase("appsettings.Development.json", ExplorerIconKind.Data)]
    [TestCase("MainWindow.axaml", ExplorerIconKind.Markup)]
    [TestCase("Directory.Build.props", ExplorerIconKind.Markup)]
    [TestCase(".editorconfig", ExplorerIconKind.Config)]
    [TestCase("global.json", ExplorerIconKind.Config)]
    [TestCase("NuGet.Config", ExplorerIconKind.Config)]
    [TestCase("README.md", ExplorerIconKind.Document)]
    [TestCase("Dockerfile", ExplorerIconKind.Container)]
    [TestCase("Dockerfile.dev", ExplorerIconKind.Container)]
    [TestCase("docker-compose.yml", ExplorerIconKind.Container)]
    [TestCase("compose.yaml", ExplorerIconKind.Container)]
    [TestCase("logo.svg", ExplorerIconKind.Image)]
    [TestCase("archive.bin", ExplorerIconKind.File)]
    public void FileNamesUseExpectedIcons(string name, ExplorerIconKind expectedIcon)
    {
        var viewModel = CreateNode(WorkspaceNodeKind.File, name);

        Assert.That(viewModel.IconKind, Is.EqualTo(expectedIcon));
    }

    [Test]
    public void FolderIconStateTracksExpansion()
    {
        var viewModel = CreateNode(WorkspaceNodeKind.Folder, "src");

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsFolderClosed, Is.True);
            Assert.That(viewModel.IsFolderOpen, Is.False);
        });

        viewModel.IsExpanded = true;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsFolderClosed, Is.False);
            Assert.That(viewModel.IsFolderOpen, Is.True);
        });
    }

    [Test]
    public void PlaceholderDoesNotExposeAnExplorerIcon()
    {
        var node = new WorkspaceNode(string.Empty, "Loading…", WorkspaceNodeKind.File);
        var viewModel = new WorkspaceNodeViewModel(node, isPlaceholder: true);

        Assert.That(viewModel.IconKind, Is.EqualTo(ExplorerIconKind.None));
    }

    private static WorkspaceNodeViewModel CreateNode(WorkspaceNodeKind kind, string name) =>
        new(new WorkspaceNode(name, name, kind));
}
