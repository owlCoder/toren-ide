using NUnit.Framework;
using Toren.App.Diagnostics.Models;
using Toren.App.Diagnostics.ViewModels;
using Toren.Language.CSharp.Models;

namespace Toren.UnitTests.Diagnostics;

[TestFixture]
public sealed class ProblemDiagnosticsViewModelTests
{
    [Test]
    public void WorkspaceProblemSurvivesLiveDocumentRefresh()
    {
        var documentPath = Path.GetFullPath("Program.cs");
        var problems = new ProblemsViewModel();
        problems.ReplaceWorkspace(new WorkspaceDiagnosticsSnapshot(
            [new CSharpDocumentDiagnostics(documentPath, [])],
            [new ProblemDiagnostic(
                "workspace.project.metadata.evaluate.failed",
                "Could not evaluate project metadata.",
                ProblemSeverity.Error,
                "Project system")]));

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
            Assert.That(problems.Count, Is.EqualTo(2));
            Assert.That(problems.Items.Any(item => item.Code == "workspace.project.metadata.evaluate.failed"), Is.True);
            Assert.That(problems.Items.Any(item => item.Code == "CS1002"), Is.True);
            Assert.That(
                problems.Items.Single(item => item.Code == "workspace.project.metadata.evaluate.failed").CanNavigate,
                Is.False);
        });
    }

    [Test]
    public void ProjectProblemParticipatesInProjectScopeWithoutDocumentLocation()
    {
        var projectPath = Path.GetFullPath(Path.Combine("App", "App.csproj"));
        var documentPath = Path.GetFullPath(Path.Combine("App", "Program.cs"));
        var otherPath = Path.GetFullPath(Path.Combine("Other", "Other.cs"));
        var problems = new ProblemsViewModel();
        problems.ReplaceWorkspace(new WorkspaceDiagnosticsSnapshot(
            [new CSharpDocumentDiagnostics(
                otherPath,
                [new CSharpDiagnostic(
                    "CS1002",
                    "; expected",
                    CSharpDiagnosticSeverity.Error,
                    1,
                    1,
                    1,
                    1)])],
            [new ProblemDiagnostic(
                "NU1101",
                "Unable to find package Demo.Package.",
                ProblemSeverity.Error,
                "NuGet",
                ProjectPath: projectPath)]));
        problems.SetScopeContext(new ProblemsScopeContext(
            documentPath,
            "App",
            [documentPath],
            projectPath));

        problems.Scope = ProblemsScope.Project;

        Assert.Multiple(() =>
        {
            Assert.That(problems.Count, Is.EqualTo(1));
            Assert.That(problems.Items.Single().Code, Is.EqualTo("NU1101"));
            Assert.That(problems.Items.Single().FileName, Is.EqualTo("NuGet"));
            Assert.That(problems.Items.Single().Location, Is.Empty);
        });
    }
}
