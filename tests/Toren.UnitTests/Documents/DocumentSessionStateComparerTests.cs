using NUnit.Framework;
using Toren.App.Documents.Models;
using Toren.App.Documents.Services;

namespace Toren.UnitTests.Documents;

[TestFixture]
public sealed class DocumentSessionStateComparerTests
{
    [Test]
    public void EquivalentSessionsMatch()
    {
        var path = Path.Combine(Path.GetTempPath(), "Program.cs");
        var left = new DocumentSessionState(
            [path],
            path,
            [new DocumentRecoverySnapshot(path, "dirty", TextDocumentEncoding.Utf8)]);
        var right = new DocumentSessionState(
            [path],
            path,
            [new DocumentRecoverySnapshot(path, "dirty", TextDocumentEncoding.Utf8)]);

        Assert.That(DocumentSessionStateComparer.AreEquivalent(left, right), Is.True);
    }

    [TestCase("different text")]
    [TestCase("")]
    public void RecoveryTextChangeDoesNotMatch(string changedText)
    {
        var path = Path.Combine(Path.GetTempPath(), "Program.cs");
        var left = new DocumentSessionState(
            [path],
            path,
            [new DocumentRecoverySnapshot(path, "dirty", TextDocumentEncoding.Utf8)]);
        var right = new DocumentSessionState(
            [path],
            path,
            [new DocumentRecoverySnapshot(path, changedText, TextDocumentEncoding.Utf8)]);

        Assert.That(DocumentSessionStateComparer.AreEquivalent(left, right), Is.False);
    }

    [Test]
    public void ActiveDocumentChangeDoesNotMatch()
    {
        var first = Path.Combine(Path.GetTempPath(), "Program.cs");
        var second = Path.Combine(Path.GetTempPath(), "Other.cs");
        var left = new DocumentSessionState([first, second], first);
        var right = new DocumentSessionState([first, second], second);

        Assert.That(DocumentSessionStateComparer.AreEquivalent(left, right), Is.False);
    }

    [Test]
    public void LegacyNullRecoveryAndEmptyRecoveryMatch()
    {
        var path = Path.Combine(Path.GetTempPath(), "Program.cs");
        var left = new DocumentSessionState([path], path, null);
        var right = new DocumentSessionState([path], path, []);

        Assert.That(DocumentSessionStateComparer.AreEquivalent(left, right), Is.True);
    }
}
