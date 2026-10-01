namespace Toren.Workspaces.Models;

/// <param name="Value">Changes when any tracked input changes.</param>
/// <param name="LastWriteTimeUtc">The most recent modification among the tracked inputs.</param>
public readonly record struct ProjectInputStamp(string Value, DateTime LastWriteTimeUtc);
