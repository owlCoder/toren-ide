using NUnit.Framework;
using Toren.App.Documents.Contracts;
using Toren.App.Documents.Models;
using Toren.App.Layout.Models;
using Toren.App.ViewModels;
using Toren.Core.Results;

namespace Toren.UnitTests.Editor;

/// <summary>In-memory store: every path "loads" a small text file.</summary>
internal sealed class FakeTextDocumentStore : ITextDocumentStore
{
    public Task<Result<TextDocumentContent>> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success(new TextDocumentContent(path, $"content of {System.IO.Path.GetFileName(path)}", default!)));

    // Adjust the return type here if ITextDocumentStore.SaveAsync returns something else.
    public Task<Result<TextDocumentContent>> SaveAsync(TextDocumentContent content, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success(content));
}

[TestFixture]
public sealed class DocumentHostViewModelTests
{
    private DocumentHostViewModel _host = null!;

    [SetUp]
    public void SetUp() => _host = new DocumentHostViewModel(new FakeTextDocumentStore());

    private static string P(string name) => Path.Combine(Path.GetTempPath(), "toren-tests", "docs", name);

    private async Task<OpenDocumentViewModel> OpenAsync(string name, bool preview = false)
    {
        var opened = await _host.OpenAsync(P(name), preview: preview);
        Assert.That(opened.IsSuccess, Is.True);
        return opened.Value!;
    }

    private IEnumerable<string> Titles => _host.OpenDocuments.Select(document => document.Title);

    // ---- opening, preview ----

    [Test]
    public async Task Open_ActivatesTheDocumentInThePrimaryEditor()
    {
        var a = await OpenAsync("a.cs");

        Assert.That(_host.ActiveDocument, Is.SameAs(a));
        Assert.That(_host.PrimaryDocument, Is.SameAs(a));
        Assert.That(a.IsActive, Is.True);
    }

    [Test]
    public async Task OpeningTheSameFileTwiceDoesNotDuplicateTheTab()
    {
        await OpenAsync("a.cs");
        await OpenAsync("a.cs");

        Assert.That(_host.OpenDocuments, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task PreviewTabIsReplacedByTheNextPreview()
    {
        await OpenAsync("a.cs", preview: true);
        await OpenAsync("b.cs", preview: true);

        Assert.That(Titles, Is.EqualTo(new[] { "b.cs" }));
        Assert.That(_host.OpenDocuments[0].IsPreview, Is.True);
        Assert.That(_host.PrimaryDocument!.Title, Is.EqualTo("b.cs"));
    }

    [Test]
    public async Task OpeningAPreviewedFileNormallyKeepsItOpen()
    {
        await OpenAsync("a.cs", preview: true);
        await OpenAsync("a.cs");
        await OpenAsync("b.cs", preview: true);

        Assert.That(Titles, Is.EqualTo(new[] { "a.cs", "b.cs" }));
        Assert.That(_host.OpenDocuments[0].IsPreview, Is.False);
    }

    [Test]
    public async Task EditingAPreviewTabMakesItPermanent()
    {
        var a = await OpenAsync("a.cs", preview: true);

        a.Text = "changed";

        Assert.That(a.IsDirty, Is.True);
        Assert.That(a.IsPreview, Is.False);
    }

    // ---- pinning ----

    [Test]
    public async Task PinnedTabsFormABlockOnTheLeft()
    {
        await OpenAsync("a.cs");
        await OpenAsync("b.cs");
        var c = await OpenAsync("c.cs");

        _host.SetPinned(c, true);
        Assert.That(Titles, Is.EqualTo(new[] { "c.cs", "a.cs", "b.cs" }));

        _host.SetPinned(_host.OpenDocuments[2], true); // b.cs
        Assert.That(Titles, Is.EqualTo(new[] { "c.cs", "b.cs", "a.cs" }));

        _host.SetPinned(c, false);
        Assert.That(Titles, Is.EqualTo(new[] { "b.cs", "c.cs", "a.cs" }));
    }

    [Test]
    public async Task PinnedTabCannotBeClosedUntilUnpinned()
    {
        var a = await OpenAsync("a.cs");
        _host.SetPinned(a, true);

        Assert.That(_host.TryClose(a), Is.False);

        _host.SetPinned(a, false);
        Assert.That(_host.TryClose(a), Is.True);
        Assert.That(_host.OpenDocuments, Is.Empty);
    }

    [Test]
    public async Task DirtyTabCannotBeClosed()
    {
        var a = await OpenAsync("a.cs");
        a.Text = "edited";

        Assert.That(_host.TryClose(a), Is.False);
    }

    // ---- split ----

    [Test]
    public void SetSplit_WithoutDocumentsDoesNothing()
    {
        _host.SetSplit(EditorSplitOrientation.Vertical);

        Assert.That(_host.IsSplit, Is.False);
    }

    [Test]
    public async Task SetSplitShowsTheSameDocumentInBothEditors()
    {
        var a = await OpenAsync("a.cs");

        _host.SetSplit(EditorSplitOrientation.Vertical);

        Assert.That(_host.IsSplit, Is.True);
        Assert.That(_host.SecondaryDocument, Is.SameAs(a));
        Assert.That(_host.SplitOrientation, Is.EqualTo(EditorSplitOrientation.Vertical));
    }

    [Test]
    public async Task ActivatingATabGoesToTheFocusedEditor()
    {
        var a = await OpenAsync("a.cs");
        var b = await OpenAsync("b.cs");
        _host.Activate(a);
        _host.SetSplit(EditorSplitOrientation.Horizontal);

        _host.FocusPane(secondary: true);
        _host.Activate(b);

        Assert.That(_host.PrimaryDocument, Is.SameAs(a));
        Assert.That(_host.SecondaryDocument, Is.SameAs(b));
        Assert.That(_host.ActiveDocument, Is.SameAs(b));
    }

    [Test]
    public async Task CloseSplit_KeepsTheDocumentOfTheFocusedEditor()
    {
        var a = await OpenAsync("a.cs");
        var b = await OpenAsync("b.cs");
        _host.Activate(a);
        _host.SetSplit(EditorSplitOrientation.Vertical);
        _host.FocusPane(secondary: true);
        _host.Activate(b);

        _host.CloseSplit();

        Assert.That(_host.IsSplit, Is.False);
        Assert.That(_host.SecondaryDocument, Is.Null);
        Assert.That(_host.PrimaryDocument, Is.SameAs(b));
        Assert.That(_host.ActiveDocument, Is.SameAs(b));
    }

    [Test]
    public async Task ClosingTheDocumentOfTheSecondEditor_ShowsANeighbourThere()
    {
        await OpenAsync("a.cs");
        await OpenAsync("b.cs");
        var c = await OpenAsync("c.cs");
        _host.SetSplit(EditorSplitOrientation.Vertical);
        _host.FocusPane(secondary: true);
        var a = _host.OpenDocuments[0];
        _host.Activate(a);

        Assert.That(_host.TryClose(a), Is.True);

        Assert.That(_host.SecondaryDocument!.Title, Is.EqualTo("b.cs"));
        Assert.That(_host.PrimaryDocument, Is.SameAs(c));
    }

    [Test]
    public async Task ClosingTheLastDocument_ClosesTheSplit()
    {
        var a = await OpenAsync("a.cs");
        _host.SetSplit(EditorSplitOrientation.Vertical);

        _host.TryClose(a);

        Assert.That(_host.IsSplit, Is.False);
        Assert.That(_host.PrimaryDocument, Is.Null);
        Assert.That(_host.SecondaryDocument, Is.Null);
    }

    // ---- layout ----

    [Test]
    public async Task ApplyLayout_RestoresPinsAndSplit()
    {
        await OpenAsync("a.cs");
        var b = await OpenAsync("b.cs");
        var layout = new WorkspaceLayoutState(
            SplitOrientation: EditorSplitOrientation.Vertical,
            SecondaryDocumentPath: b.Path,
            PinnedDocumentPaths: [b.Path]);

        _host.ApplyLayout(layout);

        Assert.That(_host.OpenDocuments[0], Is.SameAs(b));
        Assert.That(b.IsPinned, Is.True);
        Assert.That(_host.IsSplit, Is.True);
        Assert.That(_host.SecondaryDocument, Is.SameAs(b));
    }

    [Test]
    public async Task ApplyLayout_WithNoSplit_ClosesAnExistingSplit()
    {
        await OpenAsync("a.cs");
        _host.SetSplit(EditorSplitOrientation.Horizontal);

        _host.ApplyLayout(WorkspaceLayoutState.Default);

        Assert.That(_host.IsSplit, Is.False);
    }

    // ---- breadcrumbs ----

    [Test]
    public async Task ChangingTheWorkspaceRoot_UpdatesBreadcrumbsOfOpenDocuments()
    {
        var a = await OpenAsync("a.cs");

        _host.WorkspaceRoot = Path.Combine(Path.GetTempPath(), "toren-tests");

        Assert.That(a.Breadcrumbs.Select(segment => segment.Name), Is.EqualTo(new[] { "toren-tests", "docs", "a.cs" }));
    }
}

[TestFixture]
public sealed class OpenDocumentViewModelTests
{
    private static OpenDocumentViewModel Create(string text = "hello") =>
        new(new TextDocumentContent(Path.Combine(Path.GetTempPath(), "toren-tests", "x.cs"), text, default!));

    [Test]
    public void NewDocument_IsNotDirty()
    {
        Assert.That(Create().IsDirty, Is.False);
    }

    [Test]
    public void ChangingTheText_MarksItDirtyAndMarkSavedClearsIt()
    {
        var document = Create();

        document.Text = "changed";
        Assert.That(document.IsDirty, Is.True);

        document.MarkSaved();
        Assert.That(document.IsDirty, Is.False);
    }

    [Test]
    public void EditsMadeInTheSharedTextDocument_AreVisibleThroughText()
    {
        var document = Create("abc");

        document.Document.Insert(3, "def");

        Assert.That(document.Text, Is.EqualTo("abcdef"));
        Assert.That(document.IsDirty, Is.True);
    }

    [Test]
    public async Task Text_CanBeReadFromABackgroundThread()
    {
        // Regression: TextDocument.Text throws "Call from invalid thread" off the UI thread.
        var document = Create("abc");

        var text = await Task.Run(() => document.Text);

        Assert.That(text, Is.EqualTo("abc"));
    }
}
