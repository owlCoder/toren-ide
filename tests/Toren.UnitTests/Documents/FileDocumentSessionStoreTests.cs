using NUnit.Framework;
using Toren.App.Documents.Adapters;
using Toren.App.Documents.Models;

namespace Toren.UnitTests.Documents;

[TestFixture]
public sealed class FileDocumentSessionStoreTests
{
    [Test]
    public async Task SaveAndLoadRoundTripsOpenAndActiveDocuments()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var sessionPath = Path.Combine(root, "document-session.json");
            var first = Path.Combine(root, "Program.cs");
            var second = Path.Combine(root, "appsettings.json");
            var store = new FileDocumentSessionStore(sessionPath);

            var saved = await store.SaveAsync(new DocumentSessionState([first, second], second));
            var loaded = await store.LoadAsync();
            var restored = loaded.Value!;

            Assert.Multiple(() =>
            {
                Assert.That(saved.IsSuccess, Is.True);
                Assert.That(loaded.IsSuccess, Is.True);
                Assert.That(restored.OpenDocumentPaths, Is.EqualTo(new[] { first, second }));
                Assert.That(restored.ActiveDocumentPath, Is.EqualTo(second));
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task InvalidStoredSessionReturnsExplicitFailure()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var sessionPath = Path.Combine(root, "document-session.json");
            await File.WriteAllTextAsync(sessionPath, "{not-json");
            var store = new FileDocumentSessionStore(sessionPath);

            var loaded = await store.LoadAsync();

            Assert.Multiple(() =>
            {
                Assert.That(loaded.IsFailure, Is.True);
                Assert.That(loaded.Error.Code, Is.EqualTo("document.session.invalid"));
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"toren-document-session-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
