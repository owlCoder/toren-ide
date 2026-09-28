using NUnit.Framework;
using Toren.Language.CSharp.Models;
using Toren.Language.CSharp.Services;

namespace Toren.UnitTests.Language;

[TestFixture]
public sealed class RoslynCSharpCompletionServiceTests
{
    private const string CaretMarker = "/*caret*/";

    [Test]
    public async Task GetCompletionsAsyncIncludesVisibleLocalAndKeywords()
    {
        var (context, line, column) = CreateContext(
            """
            class Sample
            {
                void Run()
                {
                    int localValue = 42;
                    /*caret*/
                }
            }
            """);
        var service = new RoslynCSharpCompletionService();

        var items = await service.GetCompletionsAsync(context, line, column);

        Assert.Multiple(() =>
        {
            Assert.That(items.Any(item => item.DisplayText == "localValue"), Is.True);
            Assert.That(items.Any(item => item.DisplayText == "return"), Is.True);
        });
    }

    [Test]
    public async Task GetCompletionsAsyncUsesMemberTypeAfterDot()
    {
        var (context, line, column) = CreateContext(
            """
            class Sample
            {
                void Run()
                {
                    string value = "toren";
                    _ = value./*caret*/
                }
            }
            """);
        var service = new RoslynCSharpCompletionService();

        var items = await service.GetCompletionsAsync(context, line, column);

        Assert.Multiple(() =>
        {
            Assert.That(items.Any(item => item.DisplayText == "Length"), Is.True);
            Assert.That(items.Any(item => item.DisplayText == "ToUpper"), Is.True);
        });
    }

    [Test]
    public async Task InstanceMemberCompletionExcludesStaticMembers()
    {
        var (context, line, column) = CreateContext(
            """
            class Helper
            {
                public static int StaticValue => 1;
                public int InstanceValue => 2;
            }

            class Sample
            {
                void Run()
                {
                    var helper = new Helper();
                    _ = helper./*caret*/
                }
            }
            """);
        var service = new RoslynCSharpCompletionService();

        var items = await service.GetCompletionsAsync(context, line, column);

        Assert.Multiple(() =>
        {
            Assert.That(items.Any(item => item.DisplayText == "InstanceValue"), Is.True);
            Assert.That(items.Any(item => item.DisplayText == "StaticValue"), Is.False);
        });
    }

    [Test]
    public async Task StaticMemberCompletionExcludesInstanceMembers()
    {
        var (context, line, column) = CreateContext(
            """
            class Helper
            {
                public static int StaticValue => 1;
                public int InstanceValue => 2;
            }

            class Sample
            {
                void Run()
                {
                    _ = Helper./*caret*/
                }
            }
            """);
        var service = new RoslynCSharpCompletionService();

        var items = await service.GetCompletionsAsync(context, line, column);

        Assert.Multiple(() =>
        {
            Assert.That(items.Any(item => item.DisplayText == "StaticValue"), Is.True);
            Assert.That(items.Any(item => item.DisplayText == "InstanceValue"), Is.False);
        });
    }

    [Test]
    public async Task GetCompletionsAsyncRanksTypedPrefixBeforeOtherVisibleSymbols()
    {
        var (context, line, column) = CreateContext(
            """
            class Sample
            {
                void Run()
                {
                    int alphaValue = 1;
                    int zebraValue = 2;
                    z/*caret*/
                }
            }
            """);
        var service = new RoslynCSharpCompletionService();

        var items = await service.GetCompletionsAsync(context, line, column);

        var zebraIndex = Array.FindIndex(items.ToArray(), item => item.DisplayText == "zebraValue");
        var alphaIndex = Array.FindIndex(items.ToArray(), item => item.DisplayText == "alphaValue");
        Assert.Multiple(() =>
        {
            Assert.That(zebraIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(alphaIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(zebraIndex, Is.LessThan(alphaIndex));
        });
    }

    [Test]
    public async Task GetCompletionsAsyncSummarizesMethodOverloads()
    {
        var (context, line, column) = CreateContext(
            """
            class Sample
            {
                void Run()
                {
                    string value = "toren";
                    _ = value./*caret*/
                }
            }
            """);
        var service = new RoslynCSharpCompletionService();

        var items = await service.GetCompletionsAsync(context, line, column);

        var indexOf = items.First(item => item.DisplayText == "IndexOf");
        Assert.That(indexOf.Detail, Does.Contain("overload"));
    }

    private static (CSharpSemanticContext Context, int Line, int Column) CreateContext(string markedSource)
    {
        var markerOffset = markedSource.IndexOf(CaretMarker, StringComparison.Ordinal);
        Assert.That(markerOffset, Is.GreaterThanOrEqualTo(0));
        var source = markedSource.Remove(markerOffset, CaretMarker.Length);
        var beforeCaret = source[..markerOffset];
        var line = beforeCaret.Count(character => character == '\n') + 1;
        var lastLineBreak = beforeCaret.LastIndexOf('\n');
        var column = markerOffset - lastLineBreak;
        var context = new CSharpSemanticContext(
            "test.cs",
            [new CSharpSourceDocument("test.cs", source)]);
        return (context, line, column);
    }
}
