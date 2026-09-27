using System.Text;
using NUnit.Framework;
using Toren.App.Documents.Adapters;
using Toren.App.Documents.Models;

namespace Toren.UnitTests.Documents;

[TestFixture]
public sealed class FileTextDocumentStoreTests
{
    [Test]
    public async Task LoadsAndSavesUtf8WithoutChangingEncoding()
    {
        var path = CreateTemporaryFile(Encoding.UTF8.GetBytes("class Program {}"));
        try
        {
            var store = new FileTextDocumentStore();

            var loaded = await store.LoadAsync(path);
            Assert.Multiple(() =>
            {
                Assert.That(loaded.IsSuccess, Is.True);
                Assert.That(loaded.Value!.Text, Is.EqualTo("class Program {}"));
                Assert.That(loaded.Value.Encoding, Is.EqualTo(TextDocumentEncoding.Utf8));
            });

            var saved = await store.SaveAsync(loaded.Value! with { Text = "class Program { }" });

            Assert.Multiple(() =>
            {
                Assert.That(saved.IsSuccess, Is.True);
                Assert.That(File.ReadAllText(path), Is.EqualTo("class Program { }"));
                Assert.That(File.ReadAllBytes(path).Take(3), Is.Not.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }));
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task PreservesUtf8BomWhenSaving()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("hello")).ToArray();
        var path = CreateTemporaryFile(bytes);
        try
        {
            var store = new FileTextDocumentStore();
            var loaded = await store.LoadAsync(path);

            Assert.That(loaded.IsSuccess, Is.True);
            Assert.That(loaded.Value!.Encoding, Is.EqualTo(TextDocumentEncoding.Utf8Bom));

            await store.SaveAsync(loaded.Value with { Text = "updated" });
            var savedBytes = File.ReadAllBytes(path);

            Assert.That(savedBytes.AsSpan(0, 3).SequenceEqual(Encoding.UTF8.GetPreamble()), Is.True);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task RejectsBinaryContent()
    {
        var path = CreateTemporaryFile([0x41, 0x00, 0x42]);
        try
        {
            var store = new FileTextDocumentStore();

            var result = await store.LoadAsync(path);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsFailure, Is.True);
                Assert.That(result.Error.Code, Is.EqualTo("document.binary.unsupported"));
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreateTemporaryFile(byte[] content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"toren-document-{Guid.NewGuid():N}.txt");
        File.WriteAllBytes(path, content);
        return path;
    }
}
