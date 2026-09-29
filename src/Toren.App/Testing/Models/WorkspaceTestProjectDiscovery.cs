using Toren.DotNet.Testing.Models;

namespace Toren.App.Testing.Models;

public sealed record WorkspaceTestProjectDiscovery(
    string ProjectPath,
    string DisplayName,
    IReadOnlyList<DotNetTestCase> Tests,
    string? ErrorMessage = null)
{
    public bool Succeeded => string.IsNullOrWhiteSpace(ErrorMessage);
}
