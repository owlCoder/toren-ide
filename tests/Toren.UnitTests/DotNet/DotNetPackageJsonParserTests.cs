using NUnit.Framework;
using Toren.DotNet.Packages.Parsers;

namespace Toren.UnitTests.DotNet;

[TestFixture]
public sealed class DotNetPackageJsonParserTests
{
    [Test]
    public void SearchParsesPackagesAcrossSources()
    {
        const string json = """
            {
              "version": 2,
              "problems": [],
              "searchResult": [
                {
                  "sourceName": "https://api.nuget.org/v3/index.json",
                  "packages": [
                    {
                      "id": "Newtonsoft.Json",
                      "latestVersion": "13.0.3",
                      "totalDownloads": 4456137550,
                      "owners": "dotnetfoundation, jamesnk"
                    }
                  ]
                }
              ]
            }
            """;

        var packages = DotNetPackageJsonParser.ParseSearch(json);

        Assert.Multiple(() =>
        {
            Assert.That(packages, Has.Count.EqualTo(1));
            Assert.That(packages[0].Id, Is.EqualTo("Newtonsoft.Json"));
            Assert.That(packages[0].LatestVersion, Is.EqualTo("13.0.3"));
            Assert.That(packages[0].TotalDownloads, Is.EqualTo(4456137550L));
            Assert.That(packages[0].Owners, Is.EqualTo("dotnetfoundation, jamesnk"));
            Assert.That(packages[0].SourceName, Is.EqualTo("https://api.nuget.org/v3/index.json"));
        });
    }

    [Test]
    public void InstalledParsesTopLevelPackagesAcrossFrameworks()
    {
        const string json = """
            {
              "version": 1,
              "parameters": "",
              "projects": [
                {
                  "path": "src/App/App.csproj",
                  "frameworks": [
                    {
                      "framework": "net10.0",
                      "topLevelPackages": [
                        {
                          "id": "Microsoft.Extensions.Logging",
                          "requestedVersion": "10.0.0",
                          "resolvedVersion": "10.0.0"
                        }
                      ]
                    },
                    {
                      "framework": "net9.0",
                      "topLevelPackages": [
                        {
                          "id": "Newtonsoft.Json",
                          "requestedVersion": "13.0.3",
                          "resolvedVersion": "13.0.3"
                        }
                      ]
                    }
                  ]
                }
              ]
            }
            """;

        var packages = DotNetPackageJsonParser.ParseInstalled(json);

        Assert.Multiple(() =>
        {
            Assert.That(packages, Has.Count.EqualTo(2));
            Assert.That(packages[0].ProjectPath, Is.EqualTo("src/App/App.csproj"));
            Assert.That(packages[0].TargetFramework, Is.EqualTo("net10.0"));
            Assert.That(packages[0].Id, Is.EqualTo("Microsoft.Extensions.Logging"));
            Assert.That(packages[1].TargetFramework, Is.EqualTo("net9.0"));
            Assert.That(packages[1].Id, Is.EqualTo("Newtonsoft.Json"));
        });
    }

    [Test]
    public void InstalledToleratesProjectsWithoutFrameworks()
    {
        const string json = """
            {
              "version": 1,
              "projects": [
                { "path": "src/App/App.csproj" }
              ]
            }
            """;

        var packages = DotNetPackageJsonParser.ParseInstalled(json);

        Assert.That(packages, Is.Empty);
    }
}
