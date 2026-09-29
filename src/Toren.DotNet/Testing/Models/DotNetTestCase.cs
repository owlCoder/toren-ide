namespace Toren.DotNet.Testing.Models;

public sealed record DotNetTestCase(
    string FullyQualifiedName,
    string DisplayName);
