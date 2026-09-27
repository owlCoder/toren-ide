# macOS development

Toren IDE is under active development. Local source builds are not yet Developer ID signed or notarized for distribution.

## Recommended local workflow

Clone the repository with Git and run Toren through the .NET CLI:

```bash
git clone https://github.com/owlCoder/toren-ide.git
cd toren-ide
dotnet restore Toren.slnx
dotnet build Toren.slnx
dotnet run --project src/Toren.App/Toren.App.csproj
```

This is the supported development workflow until signed/notarized preview packages are published.

## macOS reports that `Toren.App.app` is damaged

If the repository or an app bundle was downloaded through a web browser, macOS may attach the `com.apple.quarantine` extended attribute. Because current development app bundles are not yet Developer ID signed and notarized, Gatekeeper can reject the bundle and display a message that the app is damaged.

For a Toren build that you created yourself from this repository, first locate the generated bundle:

```bash
find src/Toren.App/bin -type d -name 'Toren.App.app' -print
```

Inspect its quarantine state:

```bash
xattr -l /path/to/Toren.App.app
```

If `com.apple.quarantine` is present and you trust the local build, remove that attribute from this app bundle only:

```bash
xattr -dr com.apple.quarantine /path/to/Toren.App.app
```

Then launch the app again, or use the recommended `dotnet run` command above.

Do **not** disable Gatekeeper globally (`spctl --master-disable`) as a Toren development workaround.

## Distribution requirement

Public macOS preview and stable releases must be code-signed with an appropriate Developer ID identity, use hardened runtime where required, and be notarized before distribution. Release packaging/signing is tracked as part of Toren IDE 1.0 release readiness.
