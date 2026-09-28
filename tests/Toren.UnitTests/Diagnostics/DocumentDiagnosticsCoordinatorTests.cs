using NUnit.Framework;
using Toren.App.Diagnostics.Services;
using Toren.Language.CSharp.Contracts;
using Toren.Language.CSharp.Models;

namespace Toren.UnitTests.Diagnostics;

[TestFixture]
public sealed class DocumentDiagnosticsCoordinatorTests
{
    [Test]
    public async Task LatestDebouncedRequestSupersedesPreviousRequest()
    {
        var syntaxService = new FakeCSharpSyntaxService();
        using var coordinator = new DocumentDiagnosticsCoordinator(
            new FakeCSharpDiagnosticService(),
            syntaxService,
            debounceDelay: TimeSpan.FromMilliseconds(10));

        var first = coordinator.AnalyzeLatestAsync("Program.cs", "first", debounce: true);
        var second = coordinator.AnalyzeLatestAsync("Program.cs", "second", debounce: true);

        var firstResult = await first;
        var secondResult = await second;

        Assert.Multiple(() =>
        {
            Assert.That(firstResult, Is.Null);
            Assert.That(secondResult, Has.Count.EqualTo(1));
            Assert.That(secondResult![0].Message, Is.EqualTo("second"));
        });
    }

    [Test]
    public async Task NonCSharpDocumentSkipsLanguageServices()
    {
        var syntaxService = new FakeCSharpSyntaxService();
        var diagnosticService = new FakeCSharpDiagnosticService();
        using var coordinator = new DocumentDiagnosticsCoordinator(
            diagnosticService,
            syntaxService,
            debounceDelay: TimeSpan.Zero);

        var result = await coordinator.AnalyzeLatestAsync("settings.json", "{}", debounce: false);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Empty);
            Assert.That(syntaxService.CallCount, Is.Zero);
            Assert.That(diagnosticService.CallCount, Is.Zero);
        });
    }

    [Test]
    public async Task SemanticContextRoutesAnalysisToCompilerDiagnostics()
    {
        var syntaxService = new FakeCSharpSyntaxService();
        var diagnosticService = new FakeCSharpDiagnosticService();
        var context = new CSharpSemanticContext(
            "Program.cs",
            [new CSharpSourceDocument("Program.cs", "semantic")]);
        using var coordinator = new DocumentDiagnosticsCoordinator(
            diagnosticService,
            syntaxService,
            (_, _, _) => Task.FromResult<CSharpSemanticContext?>(context),
            TimeSpan.Zero);

        var result = await coordinator.AnalyzeLatestAsync("Program.cs", "source", debounce: false);

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result![0].Message, Is.EqualTo("semantic"));
            Assert.That(diagnosticService.CallCount, Is.EqualTo(1));
            Assert.That(syntaxService.CallCount, Is.Zero);
        });
    }

    private sealed class FakeCSharpSyntaxService : ICSharpSyntaxService
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<CSharpDiagnostic>> AnalyzeAsync(
            string sourceText,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult<IReadOnlyList<CSharpDiagnostic>>(
                [CreateDiagnostic(sourceText)]);
        }
    }

    private sealed class FakeCSharpDiagnosticService : ICSharpDiagnosticService
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<CSharpDiagnostic>> AnalyzeAsync(
            CSharpSemanticContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            var message = context.Documents.First(document =>
                document.Path.Equals(context.ActiveDocumentPath, StringComparison.Ordinal)).Text;
            return Task.FromResult<IReadOnlyList<CSharpDiagnostic>>(
                [CreateDiagnostic(message)]);
        }
    }

    private static CSharpDiagnostic CreateDiagnostic(string message) =>
        new(
            "CS0000",
            message,
            CSharpDiagnosticSeverity.Error,
            1,
            1,
            1,
            2);
}
