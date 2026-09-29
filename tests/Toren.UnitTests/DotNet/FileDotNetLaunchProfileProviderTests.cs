using NUnit.Framework;
using Toren.DotNet.Execution.Adapters;

namespace Toren.UnitTests.DotNet;

[TestFixture]
public sealed class FileDotNetLaunchProfileProviderTests
{
    [Test]
    public async Task MissingLaunchSettingsReturnsEmptyProfiles()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var projectPath = Path.Combine(directory, "App.csproj");
            var provider = new FileDotNetLaunchProfileProvider();

            var result = await provider.GetProfilesAsync(projectPath);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Value, Is.Empty);
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task ReadsProjectProfilesAndIgnoresOtherCommandTypes()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var projectPath = Path.Combine(directory, "App.csproj");
            var propertiesDirectory = Path.Combine(directory, "Properties");
            Directory.CreateDirectory(propertiesDirectory);
            await File.WriteAllTextAsync(
                Path.Combine(propertiesDirectory, "launchSettings.json"),
                """
                {
                  // Toren should accept the normal launchSettings dialect.
                  "profiles": {
                    "https": {
                      "commandName": "Project",
                      "launchBrowser": true,
                      "launchUrl": "swagger",
                      "applicationUrl": "https://localhost:7240;http://localhost:5240",
                      "environmentVariables": {
                        "ASPNETCORE_ENVIRONMENT": "Development"
                      },
                    },
                    "IIS Express": {
                      "commandName": "IISExpress"
                    }
                  }
                }
                """);
            var provider = new FileDotNetLaunchProfileProvider();

            var result = await provider.GetProfilesAsync(projectPath);

            Assert.That(result.IsSuccess, Is.True);
            var profile = result.Value!.Single();
            Assert.Multiple(() =>
            {
                Assert.That(profile.Name, Is.EqualTo("https"));
                Assert.That(profile.LaunchBrowser, Is.True);
                Assert.That(profile.LaunchUrl, Is.EqualTo("swagger"));
                Assert.That(profile.ApplicationUrl, Is.EqualTo("https://localhost:7240;http://localhost:5240"));
                Assert.That(profile.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"], Is.EqualTo("Development"));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task MalformedLaunchSettingsReturnsParseFailure()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var projectPath = Path.Combine(directory, "App.csproj");
            var propertiesDirectory = Path.Combine(directory, "Properties");
            Directory.CreateDirectory(propertiesDirectory);
            await File.WriteAllTextAsync(
                Path.Combine(propertiesDirectory, "launchSettings.json"),
                "{ not-json }");
            var provider = new FileDotNetLaunchProfileProvider();

            var result = await provider.GetProfilesAsync(projectPath);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsFailure, Is.True);
                Assert.That(result.Error.Code, Is.EqualTo("dotnet.launch-profile.parse.failed"));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"toren-launch-profile-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
