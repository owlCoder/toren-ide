namespace Toren.DotNet.Testing.Models;

public sealed record DotNetTestRunRequest(
    string ProjectPath,
    string? Configuration = null,
    string? TargetFramework = null,
    string? FullyQualifiedName = null);
