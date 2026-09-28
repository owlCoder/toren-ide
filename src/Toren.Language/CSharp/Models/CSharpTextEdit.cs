namespace Toren.Language.CSharp.Models;

public sealed record CSharpTextEdit(
    string FilePath,
    int StartOffset,
    int Length,
    string NewText);
