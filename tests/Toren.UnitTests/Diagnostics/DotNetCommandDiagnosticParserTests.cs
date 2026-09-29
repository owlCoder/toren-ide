using NUnit.Framework;
using Toren.App.Diagnostics.Models;
using Toren.App.Diagnostics.Services;
using Toren.DotNet.Execution.Models;
using Toren.Workspaces.Models;

namespace Toren.UnitTests.Diagnostics;

[TestFixture]
public sealed class DotNetCommandDiagnosticParserTests
{
    [Test]
    public void ParsesCompilerDiagnosticWithSourceLocation()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var projectPath = Path.Combine(directory, "App.csproj");
            var sourcePath = Path.Combine(directory, "Program.cs");
            var parser = new DotNetCommandDiagnosticParser();
            var result = new DotNetCommandResult(
                DotNetCommandKind.Build,
                1,
                $"{sourcePath}(12,7): error CS1002: ; expected [{projectPath}]",
                string.Empty);

            var diagnostics = parser.Parse(
                new WorkspaceDescriptor(projectPath, "App", WorkspaceKind.Project),
                result);

            var diagnostic = diagnostics.Single();
            Assert.Multiple(() =>
            {
                Assert.That(diagnostic.Code, Is.EqualTo("CS1002"));
                Assert.That(diagnostic.Severity, Is.EqualTo(ProblemSeverity.Error));
                Assert.That(diagnostic.Source, Is.EqualTo("MSBuild"));
                Assert.That(diagnostic.FilePath, Is.EqualTo(Path.GetFullPath(sourcePath)));
                Assert.That(diagnostic.ProjectPath, Is.EqualTo(Path.GetFullPath(projectPath)));
                Assert.That(diagnostic.StartLine, Is.EqualTo(12));
                Assert.That(diagnostic.StartColumn, Is.EqualTo(7));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void ParsesNuGetProjectDiagnosticWithoutFakeLocation()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var projectPath = Path.Combine(directory, "App.csproj");
            var parser = new DotNetCommandDiagnosticParser();
            var result = new DotNetCommandResult(
                DotNetCommandKind.Restore,
                1,
                string.Empty,
                $"{projectPath} : error NU1101: Unable to find package Demo.Package. [{projectPath}]");

            var diagnostics = parser.Parse(
                new WorkspaceDescriptor(projectPath, "App", WorkspaceKind.Project),
                result);

            var diagnostic = diagnostics.Single();
            Assert.Multiple(() =>
            {
                Assert.That(diagnostic.Code, Is.EqualTo("NU1101"));
                Assert.That(diagnostic.Source, Is.EqualTo("NuGet"));
                Assert.That(diagnostic.FilePath, Is.EqualTo(Path.GetFullPath(projectPath)));
                Assert.That(diagnostic.ProjectPath, Is.EqualTo(Path.GetFullPath(projectPath)));
                Assert.That(diagnostic.StartLine, Is.Zero);
                Assert.That(diagnostic.StartColumn, Is.Zero);
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void IgnoresNonDiagnosticBuildOutputAndDeduplicatesMatches()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var projectPath = Path.Combine(directory, "App.csproj");
            var diagnosticLine = $"{projectPath} : warning NU1900: Advisory source unavailable [{projectPath}]";
            var parser = new DotNetCommandDiagnosticParser();
            var result = new DotNetCommandResult(
                DotNetCommandKind.Restore,
                0,
                $"Determining projects to restore...{Environment.NewLine}{diagnosticLine}",
                diagnosticLine);

            var diagnostics = parser.Parse(
                new WorkspaceDescriptor(directory, "Workspace", WorkspaceKind.Folder),
                result);

            Assert.Multiple(() =>
            {
                Assert.That(diagnostics, Has.Count.EqualTo(1));
                Assert.That(diagnostics[0].Code, Is.EqualTo("NU1900"));
                Assert.That(diagnostics[0].Severity, Is.EqualTo(ProblemSeverity.Warning));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"toren-dotnet-diagnostics-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
