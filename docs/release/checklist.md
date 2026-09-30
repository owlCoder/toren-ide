# Toren IDE release checklist

Use this checklist for every public Preview or Stable release. It complements `packaging.md`, `macos-signing.md`, and `channels.md`; it does not replace platform-specific signing or smoke testing.

## 1. Select the channel and version

- Preview tag: `vMAJOR.MINOR.PATCH-preview.N`.
- Stable tag: `vMAJOR.MINOR.PATCH`.
- Confirm the release commit is the exact commit intended for distribution.
- Do not move an existing public release tag to a different commit.

## 2. Validate source and CI

- Confirm the normal CI workflow is green on Linux, Windows, and macOS.
- Confirm NUnit, xUnit, MSTest, and Microsoft Testing Platform compatibility gates are green.
- Review open release-blocking issues and known regressions.
- Confirm `docs/progress.md` does not claim a release gate that has not actually been validated.

## 3. Build release candidates

Run the `Package` workflow for the release ref/version and confirm all candidate artifacts are present:

- `linux-x64`;
- `win-x64`;
- `osx-x64`;
- `osx-arm64`.

The workflow also produces `Toren-IDE-release-manifest.json`. Treat this file as **candidate metadata**, not as a public update feed. It records the release version/channel, source commit, runtime identifiers, package filenames, and verified SHA-256 hashes. Candidate entries remain `distributionReady=false` until the release process has completed all required signing and smoke-test gates.

Verify that every package checksum in the manifest matches the packaged artifact. A checksum mismatch is a release blocker.

## 4. Complete platform release gates

### macOS

- Run the signed macOS workflow for both `osx-arm64` and `osx-x64` when both architectures are being distributed.
- Verify Developer ID signing and hardened runtime.
- Submit for notarization and require an accepted result.
- Staple the notarization ticket.
- Run `codesign --verify --deep --strict` and `spctl --assess --type execute` on the final `.app`.
- Smoke-test launching the final distributed ZIP on a supported macOS machine.

### Windows

- Apply the release-channel signing policy before public distribution when Windows signing is required.
- Verify the final ZIP contains the expected self-contained executable and dependencies.
- Smoke-test launch, workspace open, edit/save, restore/build/run, and clean shutdown on a supported Windows machine.

### Linux

- Verify the final archive preserves the executable bit on `Toren.App`.
- Smoke-test launch, workspace open, edit/save, restore/build/run, and clean shutdown on a supported Linux desktop.

## 5. Cross-platform product smoke test

For every platform being released, verify at minimum:

1. launch Toren IDE and open a standard `.sln`, `.slnx`, or `.csproj` workspace;
2. open/edit/save C# files and confirm syntax/semantic presentation remains usable;
3. restore and build through the IDE and confirm Output/Problems receive results;
4. run a startup project and stop/cancel it;
5. run at least one test and verify Test Explorer navigation;
6. verify Git status for a repository workspace;
7. open Settings and confirm persisted theme/keybinding behavior;
8. close/reopen the app and verify session/recovery behavior;
9. check keyboard focus, visible focus indicators, text contrast, clipping, and main-window layout in both supported themes.

Any crash, data-loss issue, broken core workflow, or inaccessible primary control is a release blocker.

## 6. Prepare release notes and final metadata

- Summarize user-visible changes since the previous public release.
- List known issues that are not release blockers.
- Publish SHA-256 checksums beside the **final distributed artifacts**, not only the unsigned CI candidates.
- Keep Preview and Stable metadata separate; never redirect Stable users to Preview implicitly.
- Ensure public metadata and downloads remain accessible without a Toren account.

## 7. Publish and verify

- Create the GitHub release from the immutable release tag.
- Mark Preview releases as prereleases; Stable releases must not be marked as prereleases.
- Attach only the final artifacts that passed the required platform gates.
- Attach their checksums and release notes.
- Verify every published download can be retrieved and its checksum matches the published value.
- Verify release metadata points to the tagged source commit.

## 8. After publication

- Perform one fresh-install/first-launch smoke test from the published artifacts where practical.
- Record any release-specific regression immediately in the MVP tracker or a dedicated issue.
- Do not overwrite published artifacts in place. Ship a new version/tag when distributed bytes must change.
