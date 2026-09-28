using NUnit.Framework;
using Toren.App.Diagnostics.Models;
using Toren.App.Diagnostics.ViewModels;
using Toren.Language.CSharp.Models;

namespace Toren.UnitTests.Diagnostics;

[TestFixture]
public sealed class ProblemsViewModelTests
{
    [Test]
    public void ReplaceBuildsPresentationItemsAndSeverityCounts()
    {
        var problems = new ProblemsViewModel();
        IReadOnlyList<CSharpDiagnostic> diagnostics =
        [
            new CSharpDiagnostic("CS1002", "; expected", CSharpDiagnosticSeverity.Error, 4, 12, 4, 12),
            new CSharpDiagnostic("CS0168", "Variable is declared but never used", CSharpDiagnosticSeverity.Warning, 7, 9, 7, 14),
            new CSharpDiagnostic("IDE0001", "Informational diagnostic", CSharpDiagnosticSeverity.Info, 9, 2, 9, 8),
        ];

        problems.Replace(Path.Combine(Path.GetTempPath(), "Program.cs"), diagnostics);

        Assert.Multiple(() =>
        {
            Assert.That(problems.HasItems, Is.True);
            Assert.That(problems.HasProblems, Is.True);
            Assert.That(problems.HasAnyProblems, Is.True);
            Assert.That(problems.IsEmpty, Is.False);
            Assert.That(problems.IsFilteredEmpty, Is.False);
            Assert.That(problems.Count, Is.EqualTo(3));
            Assert.That(problems.ErrorCount, Is.EqualTo(1));
            Assert.That(problems.WarningCount, Is.EqualTo(1));
            Assert.That(problems.InfoCount, Is.EqualTo(1));
            Assert.That(problems.Items[0].FileName, Is.EqualTo("Program.cs"));
            Assert.That(problems.Items[0].Location, Is.EqualTo("Ln 4, Col 12"));
        });
    }

    [Test]
    public void ReplaceFileAggregatesDiagnosticsAcrossDocuments()
    {
        var problems = new ProblemsViewModel();
        problems.ReplaceFile(
            Path.Combine(Path.GetTempPath(), "Alpha.cs"),
            [new CSharpDiagnostic("CS1002", "; expected", CSharpDiagnosticSeverity.Error, 1, 1, 1, 1)]);
        problems.ReplaceFile(
            Path.Combine(Path.GetTempPath(), "Beta.cs"),
            [new CSharpDiagnostic("CS0168", "Unused", CSharpDiagnosticSeverity.Warning, 2, 1, 2, 1)]);

        Assert.Multiple(() =>
        {
            Assert.That(problems.Count, Is.EqualTo(2));
            Assert.That(problems.ErrorCount, Is.EqualTo(1));
            Assert.That(problems.WarningCount, Is.EqualTo(1));
            Assert.That(problems.Items.Select(item => item.FileName), Is.EquivalentTo(["Alpha.cs", "Beta.cs"]));
        });
    }

    [Test]
    public void ReplaceFileRefreshesOnlyTheTargetDocument()
    {
        var problems = new ProblemsViewModel();
        var alphaPath = Path.Combine(Path.GetTempPath(), "Alpha.cs");
        var betaPath = Path.Combine(Path.GetTempPath(), "Beta.cs");
        problems.ReplaceFile(
            alphaPath,
            [new CSharpDiagnostic("CS1002", "; expected", CSharpDiagnosticSeverity.Error, 1, 1, 1, 1)]);
        problems.ReplaceFile(
            betaPath,
            [new CSharpDiagnostic("CS0168", "Unused", CSharpDiagnosticSeverity.Warning, 2, 1, 2, 1)]);

        problems.ReplaceFile(
            alphaPath,
            [new CSharpDiagnostic("IDE0001", "Info", CSharpDiagnosticSeverity.Info, 3, 1, 3, 1)]);

        Assert.Multiple(() =>
        {
            Assert.That(problems.Count, Is.EqualTo(2));
            Assert.That(problems.ErrorCount, Is.Zero);
            Assert.That(problems.WarningCount, Is.EqualTo(1));
            Assert.That(problems.InfoCount, Is.EqualTo(1));
            Assert.That(problems.Items.Any(item => item.FileName == "Beta.cs" && item.IsWarning), Is.True);
            Assert.That(problems.Items.Any(item => item.FileName == "Alpha.cs" && item.IsInfo), Is.True);
        });
    }

    [Test]
    public void RemoveFileRemovesOnlyThatDocumentsDiagnostics()
    {
        var problems = new ProblemsViewModel();
        var alphaPath = Path.Combine(Path.GetTempPath(), "Alpha.cs");
        var betaPath = Path.Combine(Path.GetTempPath(), "Beta.cs");
        problems.ReplaceFile(
            alphaPath,
            [new CSharpDiagnostic("CS1002", "; expected", CSharpDiagnosticSeverity.Error, 1, 1, 1, 1)]);
        problems.ReplaceFile(
            betaPath,
            [new CSharpDiagnostic("CS0168", "Unused", CSharpDiagnosticSeverity.Warning, 2, 1, 2, 1)]);

        problems.RemoveFile(alphaPath);

        Assert.Multiple(() =>
        {
            Assert.That(problems.Count, Is.EqualTo(1));
            Assert.That(problems.ErrorCount, Is.Zero);
            Assert.That(problems.WarningCount, Is.EqualTo(1));
            Assert.That(problems.Items.Single().FileName, Is.EqualTo("Beta.cs"));
        });
    }

    [Test]
    public void SeverityFiltersProjectVisibleItemsWithoutChangingTotals()
    {
        var problems = new ProblemsViewModel();
        problems.Replace(
            "Program.cs",
            [
                new CSharpDiagnostic("CS1002", "; expected", CSharpDiagnosticSeverity.Error, 1, 1, 1, 1),
                new CSharpDiagnostic("CS0168", "Unused", CSharpDiagnosticSeverity.Warning, 2, 1, 2, 1),
                new CSharpDiagnostic("IDE0001", "Info", CSharpDiagnosticSeverity.Info, 3, 1, 3, 1),
            ]);

        problems.ShowWarnings = false;
        problems.ShowInfo = false;

        Assert.Multiple(() =>
        {
            Assert.That(problems.Items, Has.Count.EqualTo(1));
            Assert.That(problems.Items[0].IsError, Is.True);
            Assert.That(problems.Count, Is.EqualTo(3));
            Assert.That(problems.ErrorCount, Is.EqualTo(1));
            Assert.That(problems.WarningCount, Is.EqualTo(1));
            Assert.That(problems.InfoCount, Is.EqualTo(1));
            Assert.That(problems.HasItems, Is.True);
        });
    }

    [Test]
    public void ScopeFiltersProjectAndCurrentDocumentCounts()
    {
        var problems = new ProblemsViewModel();
        var alphaPath = Path.Combine(Path.GetTempPath(), "Alpha.cs");
        var betaPath = Path.Combine(Path.GetTempPath(), "Beta.cs");
        problems.ReplaceFile(
            alphaPath,
            [new CSharpDiagnostic("CS1002", "; expected", CSharpDiagnosticSeverity.Error, 1, 1, 1, 1)]);
        problems.ReplaceFile(
            betaPath,
            [new CSharpDiagnostic("CS0168", "Unused", CSharpDiagnosticSeverity.Warning, 2, 1, 2, 1)]);
        problems.SetScopeContext(new ProblemsScopeContext(alphaPath, "AlphaProject", [alphaPath]));

        problems.Scope = ProblemsScope.Project;

        Assert.Multiple(() =>
        {
            Assert.That(problems.Count, Is.EqualTo(1));
            Assert.That(problems.ErrorCount, Is.EqualTo(1));
            Assert.That(problems.WarningCount, Is.Zero);
            Assert.That(problems.Items.Single().FileName, Is.EqualTo("Alpha.cs"));
            Assert.That(problems.ProjectScopeName, Is.EqualTo("AlphaProject"));
        });

        problems.Scope = ProblemsScope.CurrentDocument;
        Assert.That(problems.Items.Single().FileName, Is.EqualTo("Alpha.cs"));

        problems.Scope = ProblemsScope.Workspace;
        Assert.That(problems.Count, Is.EqualTo(2));
    }

    [Test]
    public void EmptySelectedScopeKeepsAggregateFilterBarAvailable()
    {
        var problems = new ProblemsViewModel();
        var alphaPath = Path.Combine(Path.GetTempPath(), "Alpha.cs");
        var betaPath = Path.Combine(Path.GetTempPath(), "Beta.cs");
        problems.ReplaceFile(
            alphaPath,
            [new CSharpDiagnostic("CS1002", "; expected", CSharpDiagnosticSeverity.Error, 1, 1, 1, 1)]);
        problems.SetScopeContext(new ProblemsScopeContext(betaPath, null, []));

        problems.Scope = ProblemsScope.CurrentDocument;

        Assert.Multiple(() =>
        {
            Assert.That(problems.HasAnyProblems, Is.True);
            Assert.That(problems.HasProblems, Is.False);
            Assert.That(problems.IsEmpty, Is.True);
            Assert.That(problems.Items, Is.Empty);
        });
    }

    [Test]
    public void LosingScopeContextFallsBackToWorkspace()
    {
        var problems = new ProblemsViewModel();
        var alphaPath = Path.Combine(Path.GetTempPath(), "Alpha.cs");
        problems.ReplaceFile(
            alphaPath,
            [new CSharpDiagnostic("CS1002", "; expected", CSharpDiagnosticSeverity.Error, 1, 1, 1, 1)]);
        problems.SetScopeContext(new ProblemsScopeContext(alphaPath, "AlphaProject", [alphaPath]));
        problems.Scope = ProblemsScope.Project;

        problems.SetScopeContext(ProblemsScopeContext.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(problems.Scope, Is.EqualTo(ProblemsScope.Workspace));
            Assert.That(problems.CanUseProjectScope, Is.False);
            Assert.That(problems.CanUseCurrentDocumentScope, Is.False);
            Assert.That(problems.Count, Is.EqualTo(1));
        });
    }

    [Test]
    public void DisablingAllSeverityFiltersProducesFilteredEmptyState()
    {
        var problems = new ProblemsViewModel();
        problems.Replace(
            "Program.cs",
            [new CSharpDiagnostic("CS1002", "; expected", CSharpDiagnosticSeverity.Error, 1, 1, 1, 1)]);

        problems.ShowErrors = false;

        Assert.Multiple(() =>
        {
            Assert.That(problems.HasProblems, Is.True);
            Assert.That(problems.HasItems, Is.False);
            Assert.That(problems.IsEmpty, Is.False);
            Assert.That(problems.IsFilteredEmpty, Is.True);
            Assert.That(problems.Items, Is.Empty);
        });
    }

    [Test]
    public void ClearResetsPanelState()
    {
        var problems = new ProblemsViewModel();
        problems.Replace(
            "Program.cs",
            [new CSharpDiagnostic("CS1002", "; expected", CSharpDiagnosticSeverity.Error, 1, 1, 1, 1)]);

        problems.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(problems.Items, Is.Empty);
            Assert.That(problems.HasProblems, Is.False);
            Assert.That(problems.HasAnyProblems, Is.False);
            Assert.That(problems.IsEmpty, Is.True);
            Assert.That(problems.IsFilteredEmpty, Is.False);
            Assert.That(problems.Count, Is.Zero);
            Assert.That(problems.ErrorCount, Is.Zero);
            Assert.That(problems.WarningCount, Is.Zero);
            Assert.That(problems.InfoCount, Is.Zero);
        });
    }
}
