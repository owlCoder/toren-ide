namespace Toren.DotNet.Testing.Models;

public sealed record DotNetTestDiscoveryRequest(
    string ProjectPath,
    string? Configuration = null,
    string? TargetFramework = null);
