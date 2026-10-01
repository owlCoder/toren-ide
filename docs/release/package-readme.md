# Toren IDE

A local development environment for .NET and ASP.NET Core.

## Start the app

- Windows x64: extract the complete ZIP into a folder, then run `Toren.App.exe`.
- Linux x64: extract the complete `.tar.gz`, then run `./Toren.App` from that folder. Keep all files together; the archive preserves executable permissions.
- macOS: choose `osx-arm64` for Apple Silicon or `osx-x64` for Intel, extract the ZIP, and copy `Toren IDE.app` into Applications.

The .NET runtime is included. A separate runtime installation is not required to launch Toren. Native operating-system libraries are still required; on Linux, see https://docs.avaloniaui.net/docs/deployment/linux.

## Develop a project

Install the .NET SDK required by your workspace and its `global.json`, plus Git for source-control tools. Docker Compose and `dotnet ef` are optional tools for the corresponding features. Open a folder, `.sln`, `.slnx`, or `.csproj` from the command bar.

Use Environment Doctor to check the SDK and optional tools. Settings opens in a separate dialog. Build, Rebuild, Clean and Run commands are available in the command bar; results appear in Output and Problems.

## Release identity and trust

`release-info.json` records the version, runtime identifier and source commit. SHA-256 files accompany the archives on GitHub Releases.

Candidate bundles from the Package workflow and Draft releases are for maintainer validation. macOS candidates have not received Developer ID signing or notarization. Public macOS releases require signing, notarization and platform validation. Draft packages do not constitute a validated public release.

## Links

- Source and documentation: https://github.com/owlCoder/toren-ide
- Public releases: https://github.com/owlCoder/toren-ide/releases
- License: Apache License 2.0; see LICENSE in this package.
