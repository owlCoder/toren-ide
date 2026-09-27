using NUnit.Framework;
using Toren.App.Documents.Contracts;
using Toren.App.Documents.Models;
using Toren.App.ViewModels;
using Toren.Core.Results;

namespace Toren.UnitTests.Documents;

[TestFixture]
public sealed class DocumentHostViewModelTests
{
    [Test]
    public async Task OpeningSamePathReusesExistingTab()
    {
        var store = new FakeTextDocumentStore();
        var host = new DocumentHostViewModel(store);
        var path = Path.Combine(Path.GetTempPath(), "Program.cs");

        var first = await host.OpenAsync(path);
        var second = await host.OpenAsync(path);

        Assert.Multiple(() =>
        {
            Assert.That(first.IsSuccess, Is.True);
            Assert.That(second.IsSuccess, Is.True);
            Assert.That(host.OpenDocuments, Has.Count.EqualTo(1));
            Assert.That(host.ActiveDocument, Is.SameAs(first.Value));
            Assert.That(host.HasDirtyDocuments, Is.False);
            Assert.That(store.LoadCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task EditingMarksDirtyAndSaveClearsDirtyState()
    {
        var store = new FakeTextDocumentStore();
        var host = new DocumentHostViewModel(store);
        var opened = await host.OpenAsync(Path.Combine(Path.GetTempPath(), "Program.cs"));
        var document = opened.Value!;

        document.Text = "changed";
        Assert.That(host.HasDirtyDocuments, Is.True);

        var saved = await host.SaveActiveAsync();

        Assert.Multiple(() =>
        {
            Assert.That(document.IsDirty, Is.False);
            Assert.That(host.HasDirtyDocuments, Is.False);
            Assert.That(saved.IsSuccess, Is.True);
            Assert.That(store.SaveCount, Is.EqualTo(1));
            Assert.That(store.LastSavedText, Is.EqualTo("changed"));
        });
    }

    [Test]
    public async Task DirtyDocumentCannotBeClosedWithoutSaving()
    {
        var host = new DocumentHostViewModel(new FakeTextDocumentStore());
        var opened = await host.OpenAsync(Path.Combine(Path.GetTempPath(), "Program.cs"));
        var document = opened.Value!;
        document.Text = "changed";

        var closed = host.TryClose(document);

        Assert.Multiple(() =>
        {
            Assert.That(closed, Is.False);
            Assert.That(host.HasDirtyDocuments, Is.True);
            Assert.That(host.OpenDocuments, Has.Count.EqualTo(1));
            Assert.That(host.ActiveDocument, Is.SameAs(document));
        });
    }

    private sealed class FakeTextDocumentStore : ITextDocumentStore
    {
        public int LoadCount { get; private set; }

        public int SaveCount { get; private set; }

        public string? LastSavedText { get; private set; }

        public Task<Result<TextDocumentContent>> LoadAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LoadCount++;
            return Task.FromResult(Result.Success(
                new TextDocumentContent(Path.GetFullPath(path), "initial", TextDocumentEncoding.Utf8)));
        }

        public Task<Result<TextDocumentContent>> SaveAsync(
            TextDocumentContent document,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SaveCount++;
            LastSavedText = document.Text;
            return Task.FromResult(Result.Success(document));
        }
    }
}
