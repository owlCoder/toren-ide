using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;
using Toren.Language.CSharp.Models;
using Toren.Language.CSharp.Services;

namespace Toren.UnitTests.Language;

/// <summary>
/// Editor requests analyze one document and reuse project state between requests. These tests
/// pin both to what analyzing the whole project from scratch reports.
/// </summary>
[TestFixture]
public sealed class RoslynIncrementalAnalysisTests
{
    private static readonly string[] Fixtures = [typeof(TestCompilationEndAnalyzer).Assembly.Location];

    [Test]
    public async Task ActiveDocumentDiagnosticsMatchWholeProjectDiagnostics()
    {
        var service = new RoslynCSharpDiagnosticService();
        CSharpSourceDocument[] documents =
        [
            new("Lonely.cs", """
                namespace Demo;
                public sealed class LonelyWidget
                {
                    public int Read(Vault vault) => vault.Secret + Missing.Value;
                }
                """),
            new("Vault.cs", """
                namespace Demo;
                public sealed class Vault
                {
                    private int Secret => 1;
                    public int Broken() => AlsoMissing.Value;
                }
                """),
        ];
        var context = new CSharpSemanticContext("Lonely.cs", documents)
        {
            ProjectPath = UniqueProject(),
            AnalyzerPaths = Fixtures,
        };

        var whole = await service.AnalyzeDocumentsAsync(context, ["Lonely.cs", "Vault.cs"]);
        var lonely = await service.AnalyzeAsync(context);
        var vault = await service.AnalyzeAsync(context with { ActiveDocumentPath = "Vault.cs" });

        Assert.Multiple(() =>
        {
            Assert.That(JsonSerializer.Serialize(lonely), Is.EqualTo(JsonSerializer.Serialize(whole[0].Diagnostics)));
            Assert.That(JsonSerializer.Serialize(vault), Is.EqualTo(JsonSerializer.Serialize(whole[1].Diagnostics)));
            // An inaccessible member, an unknown name, a per-document analyzer and an analyzer
            // that only reports once it has seen the whole compilation.
            Assert.That(lonely.Select(static diagnostic => diagnostic.Id),
                Is.SupersetOf(new[] { "CS0122", "CS0103", TestClassDeclarationAnalyzer.DiagnosticId, TestCompilationEndAnalyzer.DiagnosticId }));
            Assert.That(lonely.Single(static diagnostic => diagnostic.Id == TestCompilationEndAnalyzer.DiagnosticId).Message,
                Does.Contain("2 types"));
            Assert.That(vault.Select(static diagnostic => diagnostic.Id), Has.None.EqualTo(TestCompilationEndAnalyzer.DiagnosticId));
            Assert.That(vault.Select(static diagnostic => diagnostic.Id), Has.None.EqualTo("CS0122"));
        });
    }

    [Test]
    public async Task EditedDocumentsAreReanalyzedWhileUnchangedOnesAreReused()
    {
        var service = new RoslynCSharpDiagnosticService();
        var project = UniqueProject();
        const string program = "namespace Demo; public static class Program { public static int Get() => Shared.Value; }";
        const string shared = "namespace Demo; public static class Shared { public static int Value => 42; }";

        Task<IReadOnlyList<CSharpDiagnostic>> AnalyzeAsync(string programText, string sharedText) => service.AnalyzeAsync(
            new CSharpSemanticContext(
                "Program.cs",
                [new CSharpSourceDocument("Program.cs", programText), new CSharpSourceDocument("Shared.cs", sharedText)])
            {
                ProjectPath = project,
            });

        var original = await AnalyzeAsync(program, shared);
        // New string instances with equal content, as when files are read from disk again.
        var unchanged = await AnalyzeAsync(new StringBuilder(program).ToString(), new StringBuilder(shared).ToString());
        var otherDocumentEdited = await AnalyzeAsync(program, shared.Replace("Value", "Renamed", StringComparison.Ordinal));
        var bothEdited = await AnalyzeAsync(
            program.Replace("Value", "Renamed", StringComparison.Ordinal),
            shared.Replace("Value", "Renamed", StringComparison.Ordinal));
        var reverted = await AnalyzeAsync(program, shared);

        Assert.Multiple(() =>
        {
            Assert.That(original, Is.Empty);
            Assert.That(unchanged, Is.Empty);
            Assert.That(string.Join(",", otherDocumentEdited.Select(static diagnostic => diagnostic.Id)), Is.EqualTo("CS0117"));
            Assert.That(bothEdited, Is.Empty);
            Assert.That(reverted, Is.Empty);
        });
    }

    [Test]
    public async Task SourceGeneratorsSeeEditedSourcesAndRewrittenAdditionalFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"toren-generator-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var names = Path.Combine(directory, "members.names.txt");
            await File.WriteAllTextAsync(names, "Alpha");
            var service = new RoslynCSharpDiagnosticService();
            var project = UniqueProject();

            async Task<string> AnalyzeAsync(string expression, params string[] extraClasses)
            {
                var documents = new List<CSharpSourceDocument>
                {
                    new("Program.cs", $"public static class Program {{ public static int Get() => {expression}; }}"),
                };
                documents.AddRange(extraClasses.Select(static name =>
                    new CSharpSourceDocument($"{name}.cs", $"public sealed class {name} {{ }}")));
                var context = new CSharpSemanticContext("Program.cs", documents)
                {
                    AnalyzerPaths = Fixtures,
                    AdditionalFilePaths = [names],
                };
                var reused = await service.AnalyzeAsync(context with { ProjectPath = project });
                var fresh = await service.AnalyzeAsync(context);
                Assert.That(JsonSerializer.Serialize(reused), Is.EqualTo(JsonSerializer.Serialize(fresh)));
                return string.Join(",", reused.Where(static diagnostic => diagnostic.Id.StartsWith("CS", StringComparison.Ordinal))
                    .Select(static diagnostic => diagnostic.Id));
            }

            var fromFile = await AnalyzeAsync("Generated.FromFile.Alpha + Generated.Classes.Program");
            var fromFileAgain = await AnalyzeAsync("Generated.FromFile.Alpha + Generated.Classes.Program");
            var unknownClass = await AnalyzeAsync("Generated.Classes.Extra");
            var addedClass = await AnalyzeAsync("Generated.Classes.Extra", "Extra");
            var removedClass = await AnalyzeAsync("Generated.Classes.Extra");

            await File.WriteAllTextAsync(names, "Beta");
            File.SetLastWriteTimeUtc(names, File.GetLastWriteTimeUtc(names).AddSeconds(2));
            var staleMember = await AnalyzeAsync("Generated.FromFile.Alpha");
            var rewrittenMember = await AnalyzeAsync("Generated.FromFile.Beta");

            Assert.Multiple(() =>
            {
                Assert.That(fromFile, Is.Empty);
                Assert.That(fromFileAgain, Is.Empty);
                Assert.That(unknownClass, Is.EqualTo("CS0117"));
                Assert.That(addedClass, Is.Empty);
                Assert.That(removedClass, Is.EqualTo("CS0117"));
                Assert.That(staleMember, Is.EqualTo("CS0117"));
                Assert.That(rewrittenMember, Is.Empty);
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task ProjectsBeyondTheReuseLimitAreStillAnalyzedCorrectly()
    {
        var service = new RoslynCSharpDiagnosticService();
        var projects = Enumerable.Range(0, 7).Select(_ => UniqueProject()).ToArray();

        // Two rounds, so every project is requested again after others have displaced it.
        for (var round = 0; round < 2; round++)
        {
            for (var index = 0; index < projects.Length; index++)
            {
                var member = $"Member{index}";
                CSharpSourceDocument[] documents =
                [
                    new("Program.cs", $"public static class Program {{ public static int Get() => Shared.{member} + Shared.Member{index + 1}; }}"),
                    new("Shared.cs", $"public static class Shared {{ public static int {member} => 1; }}"),
                ];

                var diagnostics = await service.AnalyzeAsync(
                    new CSharpSemanticContext("Program.cs", documents) { ProjectPath = projects[index] });

                Assert.That(string.Join(",", diagnostics.Select(static diagnostic => diagnostic.Id)), Is.EqualTo("CS0117"));
                Assert.That(diagnostics[0].Message, Does.Contain($"Member{index + 1}"));
            }
        }
    }

    [Test]
    public async Task OverlappingRequestsForOneProjectAreConsistent()
    {
        var service = new RoslynCSharpDiagnosticService();
        var semantic = new RoslynCSharpSemanticService();
        var project = UniqueProject();
        var failures = new ConcurrentBag<string>();

        await Task.WhenAll(Enumerable.Range(0, 24).Select(index => Task.Run(async () =>
        {
            var member = $"Member{index % 3}";
            var context = new CSharpSemanticContext(
                "Program.cs",
                [
                    new CSharpSourceDocument("Program.cs", $"public static class Program {{ public static int Get() => Shared.{member}; }}"),
                    new CSharpSourceDocument("Shared.cs", "public static class Shared { public static int Member0 => 1; public static int Member1 => 1; }"),
                ])
            {
                ProjectPath = project,
                AnalyzerPaths = Fixtures,
            };

            var diagnostics = await service.AnalyzeAsync(context);
            var errors = string.Join(",", diagnostics.Where(static diagnostic => diagnostic.Id.StartsWith("CS", StringComparison.Ordinal))
                .Select(static diagnostic => diagnostic.Id));
            if (errors != (index % 3 == 2 ? "CS0117" : string.Empty))
            {
                failures.Add($"{member}: {errors}");
            }

            var symbol = await semantic.GetSymbolAsync(context, 1, 66);
            if (index % 3 != 2 && symbol?.Name != member)
            {
                failures.Add($"{member}: symbol {symbol?.Name}");
            }
        })));

        Assert.That(failures, Is.Empty);
    }

    private static string UniqueProject() =>
        Path.Combine(Path.GetTempPath(), $"toren-project-{Guid.NewGuid():N}", "App.csproj");
}

// In-process test fixtures only; they are not shipped as compiler extensions.
#pragma warning disable RS1036, RS1038, RS1041, RS1042, RS2008
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TestCompilationEndAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "TORENTEST002";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Test compilation end",
        "'{0}' was seen in a compilation of {1} types",
        "Testing",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start =>
        {
            var types = new ConcurrentBag<INamedTypeSymbol>();
            start.RegisterSymbolAction(symbol => types.Add((INamedTypeSymbol)symbol.Symbol), SymbolKind.NamedType);
            start.RegisterCompilationEndAction(end =>
            {
                foreach (var type in types.Where(static type => type.Name.StartsWith("Lonely", StringComparison.Ordinal)))
                {
                    end.ReportDiagnostic(Diagnostic.Create(Rule, type.Locations[0], type.Name, types.Count));
                }
            });
        });
    }
}

[Generator(LanguageNames.CSharp)]
public sealed class TestMemberGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // One member per word in every "*.names.txt" additional file.
        var fileMembers = context.AdditionalTextsProvider
            .Where(static text => text.Path.EndsWith(".names.txt", StringComparison.Ordinal))
            .Select(static (text, cancellationToken) => text.GetText(cancellationToken)?.ToString().Trim() ?? string.Empty)
            .Collect();
        context.RegisterSourceOutput(fileMembers, static (output, names) => output.AddSource(
            "FromFile.g.cs",
            SourceText.From(CreateClass("FromFile", names.Where(static name => name.Length > 0)), Encoding.UTF8)));

        // One member per class declared in the compilation.
        var classNames = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax,
                static (syntax, _) => ((ClassDeclarationSyntax)syntax.Node).Identifier.ValueText)
            .Collect();
        context.RegisterSourceOutput(classNames, static (output, names) => output.AddSource(
            "Classes.g.cs",
            SourceText.From(CreateClass("Classes", names.Distinct()), Encoding.UTF8)));
    }

    private static string CreateClass(string name, IEnumerable<string> members) =>
        $"// <auto-generated/>\nnamespace Generated {{ public static class {name} {{ {string.Join(" ", members.Select(static member => $"public static int {member} => 1;"))} }} }}";
}
#pragma warning restore RS1036, RS1038, RS1041, RS1042, RS2008
