using NUnit.Framework;
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
        ];

        problems.Replace(Path.Combine(Path.GetTempPath(), "Program.cs"), diagnostics);

        Assert.Multiple(() =>
        {
            Assert.That(problems.HasItems, Is.True);
            Assert.That(problems.IsEmpty, Is.False);
            Assert.That(problems.Count, Is.EqualTo(2));
            Assert.That(problems.ErrorCount, Is.EqualTo(1));
            Assert.That(problems.WarningCount, Is.EqualTo(1));
            Assert.That(problems.Items[0].FileName, Is.EqualTo("Program.cs"));
            Assert.That(problems.Items[0].Location, Is.EqualTo("Ln 4, Col 12"));
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
            Assert.That(problems.IsEmpty, Is.True);
            Assert.That(problems.Count, Is.Zero);
            Assert.That(problems.ErrorCount, Is.Zero);
            Assert.That(problems.WarningCount, Is.Zero);
        });
    }
}
