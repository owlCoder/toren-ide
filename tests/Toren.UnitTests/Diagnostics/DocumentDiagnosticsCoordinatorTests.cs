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
        var service = new FakeCSharpSyntaxService();
        using var coordinator = new DocumentDiagnosticsCoordinator(
            service,
            TimeSpan.FromMilliseconds(10));

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
    public async Task NonCSharpDocumentSkipsLanguageService()
    {
        var service = new FakeCSharpSyntaxService();
        using var coordinator = new DocumentDiagnosticsCoordinator(service, TimeSpan.Zero);

        var result = await coordinator.AnalyzeLatestAsync("settings.json", "{}", debounce: false);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Empty);
            Assert.That(service.CallCount, Is.Zero);
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
            IReadOnlyList<CSharpDiagnostic> diagnostics =
            [
                new CSharpDiagnostic(
                    "CS0000",
                    sourceText,
                    CSharpDiagnosticSeverity.Error,
                    1,
                    1,
                    1,
                    2),
            ];
            return Task.FromResult(diagnostics);
        }
    }
}
