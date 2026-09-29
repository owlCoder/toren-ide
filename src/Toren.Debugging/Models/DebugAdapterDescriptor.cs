namespace Toren.Debugging.Models;

public sealed record DebugAdapterDescriptor(
    string FileName,
    IReadOnlyList<string> Arguments,
    string DisplayName);
