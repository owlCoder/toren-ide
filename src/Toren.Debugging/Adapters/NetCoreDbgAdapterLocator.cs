using Toren.Core.Results;
using Toren.Debugging.Contracts;
using Toren.Debugging.Models;

namespace Toren.Debugging.Adapters;

public sealed class NetCoreDbgAdapterLocator : IDebugAdapterLocator
{
    public const string ConfiguredPathEnvironmentVariable = "TOREN_NETCOREDBG_PATH";

    private const string ConfiguredPathNotFoundErrorCode = "debug.adapter.configured-path-not-found";
    private const string NotFoundErrorCode = "debug.adapter.not-found";

    private readonly string? _configuredPath;
    private readonly string? _searchPath;
    private readonly bool _isWindows;

    public NetCoreDbgAdapterLocator()
        : this(
            Environment.GetEnvironmentVariable(ConfiguredPathEnvironmentVariable),
            Environment.GetEnvironmentVariable("PATH"),
            OperatingSystem.IsWindows())
    {
    }

    public NetCoreDbgAdapterLocator(
        string? configuredPath,
        string? searchPath,
        bool isWindows)
    {
        _configuredPath = configuredPath;
        _searchPath = searchPath;
        _isWindows = isWindows;
    }

    public Result<DebugAdapterDescriptor> Locate()
    {
        if (!string.IsNullOrWhiteSpace(_configuredPath))
        {
            var configuredPath = Path.GetFullPath(_configuredPath);
            if (File.Exists(configuredPath))
            {
                return Result.Success(CreateDescriptor(configuredPath));
            }

            return Result.Failure<DebugAdapterDescriptor>(
                OperationError.Create(
                    ConfiguredPathNotFoundErrorCode,
                    $"The debug adapter configured by {ConfiguredPathEnvironmentVariable} was not found at '{configuredPath}'."));
        }

        var executableName = _isWindows ? "netcoredbg.exe" : "netcoredbg";
        foreach (var directory in EnumerateSearchDirectories(_searchPath))
        {
            var candidate = Path.Combine(directory, executableName);
            if (File.Exists(candidate))
            {
                return Result.Success(CreateDescriptor(Path.GetFullPath(candidate)));
            }
        }

        return Result.Failure<DebugAdapterDescriptor>(
            OperationError.Create(
                NotFoundErrorCode,
                $"Toren could not find {executableName}. Install netcoredbg and add it to PATH, or set {ConfiguredPathEnvironmentVariable} to the debugger executable."));
    }

    private static DebugAdapterDescriptor CreateDescriptor(string path) =>
        new(path, ["--interpreter=vscode"], "netcoredbg");

    private static IEnumerable<string> EnumerateSearchDirectories(string? searchPath)
    {
        if (string.IsNullOrWhiteSpace(searchPath))
        {
            yield break;
        }

        foreach (var entry in searchPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var directory = entry.Trim().Trim('"');
            if (!string.IsNullOrWhiteSpace(directory))
            {
                yield return directory;
            }
        }
    }
}
