namespace Toren.App.Documents.Models;

public sealed record DocumentSessionState(
    string[] OpenDocumentPaths,
    string? ActiveDocumentPath,
    DocumentRecoverySnapshot[]? RecoveryDocuments = null);
