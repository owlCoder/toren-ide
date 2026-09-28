using NUnit.Framework;
using Toren.Language.CSharp.Models;
using Toren.Language.CSharp.Services;

namespace Toren.UnitTests.Language;

[TestFixture]
public sealed class RoslynCSharpDiagnosticServiceTests
{
    [Test]
    public async Task ProjectContextResolvesSymbolsFromOtherSourceDocuments()
    {
        var service = new RoslynCSharpDiagnosticService();
        var context = new CSharpSemanticContext(
            "Program.cs",
            [
                new CSharpSourceDocument(
                    "Program.cs",
                    "namespace Demo; public sealed class Program { public int Get() => Shared.Value; }"),
                new CSharpSourceDocument(
                    "Shared.cs",
                    "namespace Demo; public static class Shared { public static int Value => 42; }"),
            ]);

        var diagnostics = await service.AnalyzeAsync(context);

        Assert.That(diagnostics, Is.Empty);
    }

    [Test]
    public async Task SemanticCompilerErrorIsReturnedForActiveDocument()
    {
        var service = new RoslynCSharpDiagnosticService();
        var context = new CSharpSemanticContext(
            "Program.cs",
            [
                new CSharpSourceDocument(
                    "Program.cs",
                    "namespace Demo; public sealed class Program { public int Get() => Missing.Value; }"),
            ]);

        var diagnostics = await service.AnalyzeAsync(context);

        Assert.That(diagnostics.Any(diagnostic => diagnostic.Id == "CS0103"), Is.True);
    }

    [Test]
    public async Task DiagnosticsFromOtherDocumentsAreNotShownForActiveDocument()
    {
        var service = new RoslynCSharpDiagnosticService();
        var context = new CSharpSemanticContext(
            "Program.cs",
            [
                new CSharpSourceDocument(
                    "Program.cs",
                    "namespace Demo; public sealed class Program { public int Get() => 42; }"),
                new CSharpSourceDocument(
                    "Broken.cs",
                    "namespace Demo; public sealed class Broken { public int Get() => Missing.Value; }"),
            ]);

        var diagnostics = await service.AnalyzeAsync(context);

        Assert.That(diagnostics, Is.Empty);
    }
}
