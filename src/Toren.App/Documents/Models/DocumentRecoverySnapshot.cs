namespace Toren.App.Documents.Models;

public sealed record DocumentRecoverySnapshot(
    string Path,
    string Text,
    TextDocumentEncoding Encoding);
