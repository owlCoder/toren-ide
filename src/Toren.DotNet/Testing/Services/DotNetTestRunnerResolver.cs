using System.Text.Json;
using Toren.DotNet.Testing.Models;

namespace Toren.DotNet.Testing.Services;

public static class DotNetTestRunnerResolver
{
    public static DotNetTestRunner Resolve(string projectPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);

        var directory = Path.GetDirectoryName(Path.GetFullPath(projectPath));
        while (!string.IsNullOrWhiteSpace(directory))
        {
            var globalJsonPath = Path.Combine(directory, "global.json");
            if (File.Exists(globalJsonPath))
            {
                return ResolveFromGlobalJson(globalJsonPath);
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        return DotNetTestRunner.VSTest;
    }

    private static DotNetTestRunner ResolveFromGlobalJson(string globalJsonPath)
    {
        try
        {
            using var stream = File.OpenRead(globalJsonPath);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.TryGetProperty("test", out var test)
                && test.ValueKind == JsonValueKind.Object
                && test.TryGetProperty("runner", out var runner)
                && runner.ValueKind == JsonValueKind.String
                && runner.GetString()?.Equals(
                    "Microsoft.Testing.Platform",
                    StringComparison.OrdinalIgnoreCase) == true)
            {
                return DotNetTestRunner.MicrosoftTestingPlatform;
            }
        }
        catch (JsonException)
        {
            // Invalid global.json is handled by the dotnet SDK when commands execute.
        }
        catch (IOException)
        {
            // If the file changes during resolution, let dotnet remain authoritative.
        }

        return DotNetTestRunner.VSTest;
    }
}
