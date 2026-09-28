namespace Toren.Language.CSharp.Models;

public sealed record CSharpSourceLocation(string? FilePath, int Line, int Column)
{
    public CSharpSourceLocation(int line, int column)
        : this(null, line, column)
    {
    }
}
