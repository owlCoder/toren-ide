using NUnit.Framework;
using Toren.Language.CSharp.Models;
using Toren.Language.CSharp.Services;

namespace Toren.UnitTests.Language;

[TestFixture]
public sealed class RoslynCSharpSyntaxServiceTests
{
    [Test]
    public async Task ValidSourceHasNoSyntaxDiagnostics()
    {
        var service = new RoslynCSharpSyntaxService();

        var diagnostics = await service.AnalyzeAsync("namespace Demo; public sealed class Program { }");

        Assert.That(diagnostics, Is.Empty);
    }

    [Test]
    public async Task InvalidSourceReturnsSourceMappedError()
    {
        var service = new RoslynCSharpSyntaxService();

        var diagnostics = await service.AnalyzeAsync("class Program { void Run( { } }");

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics, Is.Not.Empty);
            Assert.That(diagnostics.Any(item => item.Severity == CSharpDiagnosticSeverity.Error), Is.True);
            Assert.That(diagnostics.All(item => item.StartLine >= 1 && item.StartColumn >= 1), Is.True);
        });
    }
}
