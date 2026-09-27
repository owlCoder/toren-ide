using System.Reflection;

namespace Toren.App.ViewModels;

public sealed class AboutWindowViewModel
{
    public AboutWindowViewModel()
    {
        ProductName = "Toren IDE";
        Version = GetProductVersion();
        Description = "A cross-platform, local-first development environment for modern .NET and ASP.NET Core.";
        License = "Apache License 2.0";
        Repository = "github.com/owlCoder/toren-ide";
    }

    public string ProductName { get; }

    public string Version { get; }

    public string Description { get; }

    public string License { get; }

    public string Repository { get; }

    private static string GetProductVersion()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version;
        return version is null ? "Development build" : version.ToString(3);
    }
}
