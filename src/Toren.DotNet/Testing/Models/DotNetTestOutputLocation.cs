namespace Toren.DotNet.Testing.Models;

public sealed record DotNetTestOutputLocation(
    string FilePath,
    int Line,
    int Column = 1);
