using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
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

    [Test]
    public async Task WorkspaceAnalysisReturnsDiagnosticsForEveryRequestedDocument()
    {
        var service = new RoslynCSharpDiagnosticService();
        var programPath = Path.GetFullPath("Program.cs");
        var brokenPath = Path.GetFullPath("Broken.cs");
        var context = new CSharpSemanticContext(
            programPath,
            [
                new CSharpSourceDocument(
                    programPath,
                    "namespace Demo; public sealed class Program { public int Get() => 42; }"),
                new CSharpSourceDocument(
                    brokenPath,
                    "namespace Demo; public sealed class Broken { public int Get() => Missing.Value; }"),
            ]);

        var diagnostics = await service.AnalyzeDocumentsAsync(context, [programPath, brokenPath]);

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics, Has.Count.EqualTo(2));
            Assert.That(diagnostics.Single(item => item.FilePath == programPath).Diagnostics, Is.Empty);
            Assert.That(
                diagnostics.Single(item => item.FilePath == brokenPath).Diagnostics.Any(item => item.Id == "CS0103"),
                Is.True);
        });
    }

    [Test]
    public async Task WorkspaceAnalysisDoesNotLeakDiagnosticsFromNonTargetDocuments()
    {
        var service = new RoslynCSharpDiagnosticService();
        var programPath = Path.GetFullPath("Program.cs");
        var referencedPath = Path.GetFullPath("Referenced.cs");
        var context = new CSharpSemanticContext(
            programPath,
            [
                new CSharpSourceDocument(
                    programPath,
                    "namespace Demo; public sealed class Program { public int Get() => 42; }"),
                new CSharpSourceDocument(
                    referencedPath,
                    "namespace Demo; public sealed class Referenced { public int Get() => Missing.Value; }"),
            ]);

        var diagnostics = await service.AnalyzeDocumentsAsync(context, [programPath]);

        Assert.That(diagnostics, Has.Count.EqualTo(1));
        Assert.That(diagnostics[0].Diagnostics, Is.Empty);
    }

    [Test]
    public async Task ProjectAnalyzerDiagnosticIsReturnedForActiveDocument()
    {
        var service = new RoslynCSharpDiagnosticService();
        var context = new CSharpSemanticContext(
            "Program.cs",
            [
                new CSharpSourceDocument(
                    "Program.cs",
                    "namespace Demo; public sealed class Program { }"),
            ])
        {
            AnalyzerPaths = [typeof(TestClassDeclarationAnalyzer).Assembly.Location],
        };

        var diagnostics = await service.AnalyzeAsync(context);

        Assert.That(diagnostics.Any(diagnostic => diagnostic.Id == TestClassDeclarationAnalyzer.DiagnosticId), Is.True);
    }
}

// This analyzer is an in-process test fixture only; it is not shipped as a compiler extension.
#pragma warning disable RS1036, RS1038, RS1041, RS2008
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TestClassDeclarationAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "TORENTEST001";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Test class declaration",
        "Class '{0}' was analyzed",
        "Testing",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeClass, SyntaxKind.ClassDeclaration);
    }

    private static void AnalyzeClass(SyntaxNodeAnalysisContext context)
    {
        var declaration = (ClassDeclarationSyntax)context.Node;
        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            declaration.Identifier.GetLocation(),
            declaration.Identifier.ValueText));
    }
}
#pragma warning restore RS1036, RS1038, RS1041, RS2008
