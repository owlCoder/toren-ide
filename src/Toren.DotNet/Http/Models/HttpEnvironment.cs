namespace Toren.DotNet.Http.Models;

public sealed record HttpEnvironment(
    string Name,
    IReadOnlyDictionary<string, string> Variables);
