namespace Toren.Language.CSharp.Models;

public sealed record CSharpRenameResult(
    string OriginalName,
    string NewName,
    IReadOnlyList<CSharpRenamedDocument> Documents);
