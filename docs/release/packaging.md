# Packaging and release artifacts

Toren IDE release artifacts are built from `src/Toren.App/Toren.App.csproj` as explicit self-contained .NET publishes. Packaging stays separate from normal CI so build/test remains fast and release packaging can be validated independently.

## Supported package RIDs

The packaging workflow currently produces:

- `win-x64` — ZIP containing the self-contained Windows publish;
- `linux-x64` — `.tar.gz` containing the self-contained Linux publish;
- `osx-x64` — ZIP containing an unsigned `Toren IDE.app` bundle for Intel Macs;
- `osx-arm64` — ZIP containing an unsigned `Toren IDE.app` bundle for Apple Silicon.

Each package is accompanied by a SHA-256 checksum file. The workflow verifies the expected app host before uploading an artifact. Runtime-specific publishes explicitly pass `--self-contained true`; relying on the RID alone is intentionally avoided.

## Platform signing policy

Public macOS Preview/Stable distribution must follow `macos-signing.md`: Developer ID signing, hardened runtime, notarization, stapling and final verification. Signing credentials must come from release secrets; they are never committed to the repository. The release-candidate manifest therefore records `requiresPlatformSigning=true` for both macOS RIDs.

Windows Authenticode/code signing is not an MVP 1.0 release gate. The Windows ZIP is still checksum-verified, smoke-tested, and bound into the same Release Validation record, but its candidate manifest entry records `requiresPlatformSigning=false`. If Windows signing becomes a release requirement later, add a dedicated signed-artifact workflow/attestation and update the promotion contract rather than flipping the flag without verification.

Linux does not require a platform-signing step in the current MVP release policy.

## macOS bundles

The package workflow creates the standard `.app` layout and an `Info.plist` with bundle identifier `dev.toren.ide`. CI artifacts are intentionally **unsigned**. They exist to validate publish and bundle composition, not to bypass Gatekeeper.

## Running the workflow

`Package` can be started manually from GitHub Actions. Changes to the packaging workflow itself also run the four-RID package matrix on `main`, which acts as the packaging regression gate.

Before publishing a release:

1. Ensure normal CI is green on macOS, Windows and Linux.
2. Run `Package` for the intended release commit.
3. Verify all four artifacts and their SHA-256 files are present.
4. Run platform signing/notarization steps required by the release policy (currently both macOS RIDs).
5. Smoke-test the final distributed artifacts on their target operating systems.
6. Publish release notes and checksums together with the signed/final artifacts.

## What this does not claim

A green packaging workflow proves that Toren can be self-contained-published and assembled into the expected archive/app-bundle shape. It does not by itself prove macOS notarization, installer UX, update delivery, or final cross-platform visual quality. Those remain separate release concerns, with notarization and the documented hands-on validation gates required for MVP 1.0. Windows signing is deliberately outside the current MVP release contract rather than an unimplemented hidden gate.
