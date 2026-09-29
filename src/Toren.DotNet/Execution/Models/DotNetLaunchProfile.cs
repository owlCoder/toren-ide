namespace Toren.DotNet.Execution.Models;

public sealed record DotNetLaunchProfile(
    string Name,
    bool LaunchBrowser,
    string? LaunchUrl,
    string? ApplicationUrl,
    IReadOnlyDictionary<string, string> EnvironmentVariables);
