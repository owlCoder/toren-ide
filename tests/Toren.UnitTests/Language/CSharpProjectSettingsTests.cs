using Microsoft.CodeAnalysis;
using NUnit.Framework;
using Toren.Language.CSharp.Models;
using Toren.Language.CSharp.Services;

namespace Toren.UnitTests.Language;

[TestFixture]
public sealed class CSharpProjectSettingsTests
{
    [Test]
    public async Task GlobalUsingsNullablePreprocessorSymbolsAndExecutableOutputMatchProjectSettings()
    {
        var context = new CSharpSemanticContext("Program.cs",
        [
            new CSharpSourceDocument("Program.cs", """
                #if FEATURE
                string? text = null;
                Console.WriteLine(text);
                #else
                MissingType value;
                #endif
                """),
            new CSharpSourceDocument("GlobalUsings.g.cs", "global using System;"),
        ]) { OutputType = "Exe", Nullable = "enable", DefineConstants = ["FEATURE"], LanguageVersion = "14.0" };

        var diagnostics = await new RoslynCSharpDiagnosticService().AnalyzeAsync(context);
        Assert.That(diagnostics, Is.Empty);
    }

    [Test]
    public async Task MultipleIncrementalGeneratorsReceiveAdditionalFilesAndProjectOptions()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"toren-generator-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var input = Path.Combine(directory, "GeneratorInput.txt");
            var config = Path.Combine(directory, "Generated.editorconfig");
            await File.WriteAllTextAsync(input, "generate");
            await File.WriteAllTextAsync(config, "is_global = true\nbuild_property.TorenGeneratorTest = true\n");
            var context = new CSharpSemanticContext("Program.cs",
                [new CSharpSourceDocument("Program.cs", "public class Consumer { public int Value => GeneratedAlpha.Value + GeneratedBeta.Value; }")])
            {
                AnalyzerPaths = [typeof(TestIncrementalGeneratorAlpha).Assembly.Location],
                AdditionalFilePaths = [input], AnalyzerConfigPaths = [config],
            };
            var diagnostics = await new RoslynCSharpDiagnosticService().AnalyzeAsync(context);
            Assert.That(diagnostics.Where(item => item.Severity == CSharpDiagnosticSeverity.Error), Is.Empty);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}

// These generators are in-process test fixtures and are not shipped as compiler extensions.
#pragma warning disable RS1036, RS1038, RS1041
[Generator(LanguageNames.CSharp)]
public sealed class TestIncrementalGeneratorAlpha : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context) => Configure(context, "Alpha");

    internal static void Configure(IncrementalGeneratorInitializationContext context, string suffix)
    {
        var input = context.AdditionalTextsProvider.Collect().Combine(context.AnalyzerConfigOptionsProvider);
        context.RegisterSourceOutput(input, (production, values) =>
        {
            if (values.Left.Length > 0 && values.Left[0].GetText()?.ToString() == "generate"
                && values.Right.GlobalOptions.TryGetValue("build_property.TorenGeneratorTest", out var enabled) && enabled == "true")
            {
                production.AddSource($"Generated{suffix}.g.cs", $"public static class Generated{suffix} {{ public static int Value => 1; }}");
            }
        });
    }
}

[Generator(LanguageNames.CSharp)]
public sealed class TestIncrementalGeneratorBeta : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context) => TestIncrementalGeneratorAlpha.Configure(context, "Beta");
}
#pragma warning restore RS1036, RS1038, RS1041
