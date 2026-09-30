using NUnit.Framework;
using Toren.App.Documents.Adapters;
using Toren.App.Documents.Models;

namespace Toren.UnitTests.Documents;

[TestFixture]
public sealed class FileDocumentSessionStoreTests
{
    [Test]
    public async Task SaveAndLoadRoundTripsOpenActiveAndRecoveryDocuments()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var sessionPath = Path.Combine(root, "document-session.json");
            var first = Path.Combine(root, "Program.cs");
            var second = Path.Combine(root, "appsettings.json");
            var store = new FileDocumentSessionStore(sessionPath);
            var recovery = new DocumentRecoverySnapshot(
                first,
                "unsaved text",
                TextDocumentEncoding.Utf8);

            var saved = await store.SaveAsync(
                new DocumentSessionState([first, second], second, [recovery]));
            var loaded = await store.LoadAsync();
            var restored = loaded.Value!;

            Assert.Multiple(() =>
            {
                Assert.That(saved.IsSuccess, Is.True);
                Assert.That(loaded.IsSuccess, Is.True);
                Assert.That(restored.OpenDocumentPaths, Is.EqualTo(new[] { first, second }));
                Assert.That(restored.ActiveDocumentPath, Is.EqualTo(second));
                Assert.That(restored.RecoveryDocuments, Is.EqualTo(new[] { recovery }));
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task LegacySessionWithoutRecoveryDocumentsStillLoads()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var sessionPath = Path.Combine(root, "document-session.json");
            var document = Path.Combine(root, "Program.cs");
            await File.WriteAllTextAsync(
                sessionPath,
                $"{{\"Version\":1,\"OpenDocumentPaths\":[\"{EscapeJson(document)}\"],\"ActiveDocumentPath\":null}}");
            var store = new FileDocumentSessionStore(sessionPath);

            var loaded = await store.LoadAsync();

            Assert.Multiple(() =>
            {
                Assert.That(loaded.IsSuccess, Is.True);
                Assert.That(loaded.Value!.OpenDocumentPaths, Is.EqualTo(new[] { document }));
                Assert.That(loaded.Value.RecoveryDocuments, Is.Empty);
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task RecoveryDocumentMustBelongToOpenSession()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var sessionPath = Path.Combine(root, "document-session.json");
            var open = Path.Combine(root, "Program.cs");
            var unrelated = Path.Combine(root, "Other.cs");
            var store = new FileDocumentSessionStore(sessionPath);

            var saved = await store.SaveAsync(
                new DocumentSessionState(
                    [open],
                    open,
                    [new DocumentRecoverySnapshot(unrelated, "dirty", TextDocumentEncoding.Utf8)]));

            Assert.Multiple(() =>
            {
                Assert.That(saved.IsSuccess, Is.True);
                Assert.That(saved.Value!.RecoveryDocuments, Is.Empty);
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

    private static string EscapeJson(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal);

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"toren-document-session-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
