using System.Reflection;

namespace Toren.App.ViewModels;

public sealed class AboutWindowViewModel
{
    public string ProductName => "Toren IDE";

    public string Version { get; } = GetProductVersion();

    public string Description => "A cross-platform, local-first development environment for modern .NET and ASP.NET Core.";

    public string License => "Open source under the Apache License 2.0.";

    public string Repository => "github.com/owlCoder/toren-ide";

    private static string GetProductVersion()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version;
        return version is null ? "Development build" : version.ToString(3);
    }
}
