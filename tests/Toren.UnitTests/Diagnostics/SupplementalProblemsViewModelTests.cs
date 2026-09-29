using NUnit.Framework;
using Toren.App.Diagnostics.Models;
using Toren.App.Diagnostics.ViewModels;
using Toren.Language.CSharp.Models;

namespace Toren.UnitTests.Diagnostics;

[TestFixture]
public sealed class SupplementalProblemsViewModelTests
{
    [Test]
    public void SupplementalDiagnosticsCoexistWithWorkspaceAndLiveDocumentProblems()
    {
        var documentPath = Path.GetFullPath("Program.cs");
        var projectPath = Path.GetFullPath("App.csproj");
        var problems = new ProblemsViewModel();
        problems.ReplaceWorkspace(new WorkspaceDiagnosticsSnapshot(
            [],
            [new ProblemDiagnostic(
                "workspace.project.graph.failed",
                "Project graph failed.",
                ProblemSeverity.Error,
                "Project system")]));
        problems.ReplaceSupplemental(
            "dotnet.command",
            [new ProblemDiagnostic(
                "NU1101",
                "Unable to find package Demo.Package.",
                ProblemSeverity.Error,
                "NuGet",
                ProjectPath: projectPath)]);
        problems.ReplaceFile(
            documentPath,
            [new CSharpDiagnostic(
                "CS1002",
                "; expected",
                CSharpDiagnosticSeverity.Error,
                2,
                4,
                2,
                4)]);

        Assert.Multiple(() =>
        {
            Assert.That(problems.Count, Is.EqualTo(3));
            Assert.That(problems.Items.Any(item => item.Code == "workspace.project.graph.failed"), Is.True);
            Assert.That(problems.Items.Any(item => item.Code == "NU1101"), Is.True);
            Assert.That(problems.Items.Any(item => item.Code == "CS1002"), Is.True);
        });
    }

    [Test]
    public void ReplacingSupplementalSourceRemovesOnlyThatSourcesPreviousDiagnostics()
    {
        var problems = new ProblemsViewModel();
        problems.ReplaceWorkspace(new WorkspaceDiagnosticsSnapshot(
            [],
            [new ProblemDiagnostic(
                "workspace.project.graph.failed",
                "Project graph failed.",
                ProblemSeverity.Error,
                "Project system")]));
        problems.ReplaceSupplemental(
            "dotnet.command",
            [new ProblemDiagnostic(
                "MSB1001",
                "Build failed.",
                ProblemSeverity.Error,
                "MSBuild")]);

        problems.ReplaceSupplemental("dotnet.command", []);

        Assert.Multiple(() =>
        {
            Assert.That(problems.Count, Is.EqualTo(1));
            Assert.That(problems.Items.Single().Code, Is.EqualTo("workspace.project.graph.failed"));
        });
    }
}
