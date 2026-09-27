namespace Toren.App.Documents.Models;

public sealed record TextDocumentContent(
    string Path,
    string Text,
    TextDocumentEncoding Encoding);
