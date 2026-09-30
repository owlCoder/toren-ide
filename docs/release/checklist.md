# Toren IDE release checklist

Use this checklist for every public Preview or Stable release. It complements `packaging.md`, `macos-signing.md`, `channels.md`, and `publishing.md`; it does not replace platform-specific signing or hands-on smoke testing.

## 1. Select the channel and version

- Preview tag: `vMAJOR.MINOR.PATCH-preview.N`.
- Stable tag: `vMAJOR.MINOR.PATCH`.
- Confirm the release commit is the exact commit intended for distribution.
- Do not move an existing public release tag to a different commit.

## 2. Validate source and CI

- Confirm the normal CI workflow is green on Linux, Windows, and macOS.
- Confirm NUnit, xUnit, MSTest, and Microsoft Testing Platform compatibility gates are green.
- Confirm the release-promotion and release-validation contract tests are green on all three CI platforms.
- Review open release-blocking issues and known regressions.
- Confirm `docs/progress.md` does not claim a release gate that has not actually been validated.

## 3. Build release candidates

Run the `Package` workflow for the release ref/version and confirm all candidate artifacts are present:

- `linux-x64`;
- `win-x64`;
- `osx-x64`;
- `osx-arm64`.

The workflow also produces `Toren-IDE-release-manifest.json`. Treat this file as **candidate metadata**, not as a public update feed. It records the release version/channel, source commit, runtime identifiers, package filenames, and verified SHA-256 hashes. Candidate entries remain `distributionReady=false` until promotion has completed the required provenance/signing checks.

Verify that every package checksum in the manifest matches the packaged artifact. A checksum mismatch, duplicate/missing RID, invalid signing policy, or pre-promoted `distributionReady=true` entry is a release blocker.

## 4. Produce signed platform artifacts

### macOS

- Run the signed macOS workflow for both `osx-arm64` and `osx-x64` when both architectures are being distributed.
- Verify Developer ID signing and hardened runtime.
- Submit for notarization and require an accepted result.
- Staple the notarization ticket.
- Run `codesign --verify --deep --strict` and `spctl --assess --type execute` on the final `.app`.
- Confirm the signed-artifact attestation matches the exact release version, source commit, RID and ZIP SHA-256.

### Windows

- Apply the release-channel signing policy before public distribution when Windows signing is required.
- Verify the final ZIP contains the expected self-contained executable and dependencies.

### Linux

- Verify the final archive preserves the executable bit on `Toren.App`.

## 5. Assemble the dry-run promotion

Run `Publish Release` with `dry_run=true` using the exact Package run plus both signed macOS runs for the immutable release tag.

- Confirm the workflow produces `Toren-IDE-release-promotion`.
- Confirm the promoted set contains exactly four normalized package artifacts, their fresh SHA-256 files, and `Toren-IDE-release-manifest.json`.
- Confirm the promoted manifest is bound to the expected release version/channel/source commit.
- Do not treat the dry-run artifact as publicly releasable until the hands-on gates below pass.

The promotion script requires an empty output destination and rejects stale files, cross-release signed artifacts, incomplete/duplicate candidate RIDs, invalid candidate policy state, and checksum/hash mismatches.

## 6. Cross-platform product smoke, visual, accessibility and performance validation

Perform these checks against the **promoted dry-run artifacts** that will later be bound into the Release Validation record.

### macOS

- Smoke-test launching the final distributed ZIP on supported Intel/Apple Silicon hardware as applicable.
- Confirm Gatekeeper accepts the app and the stapled/notarized bundle launches normally.

### Windows

- Smoke-test launch, workspace open, edit/save, restore/build/run, and clean shutdown on a supported Windows machine.

### Linux

- Smoke-test launch, workspace open, edit/save, restore/build/run, and clean shutdown on a supported Linux desktop.

### Core workflow checklist on every released platform

1. launch Toren IDE and open a standard `.sln`, `.slnx`, or `.csproj` workspace;
2. open/edit/save C# files and confirm syntax/semantic presentation remains usable;
3. restore and build through the IDE and confirm Output/Problems receive results;
4. run a startup project and stop/cancel it;
5. run at least one test and verify Test Explorer navigation;
6. verify Git status for a repository workspace;
7. open Settings and confirm persisted theme/keybinding behavior;
8. close/reopen the app and verify session/recovery behavior;
9. check keyboard focus, visible focus indicators, text contrast, clipping, and main-window layout in both supported themes;
10. verify startup, workspace opening, common editor interaction and core command execution do not show release-blocking performance regressions.

Any crash, data-loss issue, broken core workflow, inaccessible primary control, material visual regression, or release-blocking performance regression is a release blocker.

## 7. Create the Release Validation record

Only after all hands-on checks above pass, run `Release Validation` for the same immutable tag and the successful dry-run `Publish Release` workflow run ID.

Set all required confirmations to `true`:

- Windows smoke;
- Linux smoke;
- macOS smoke;
- visual/accessibility review;
- performance review.

The workflow must upload `Toren-IDE-release-validation`. Review the record and confirm it identifies:

- the expected version, channel and source commit;
- the exact dry-run promotion workflow run ID;
- the validating GitHub actor and timestamp;
- every required check as true;
- exactly one artifact for `linux-x64`, `win-x64`, `osx-x64`, and `osx-arm64`;
- the exact filename and SHA-256 for every promoted artifact.

The validation workflow re-hashes every promoted package and checksum. Do not hand-edit or reuse a validation record for a different tag, commit, promotion run, or package byte set.

## 8. Prepare release notes and final metadata

- Summarize user-visible changes since the previous public release.
- List known issues that are not release blockers.
- Publish SHA-256 checksums beside the **final distributed artifacts**, not only the unsigned CI candidates.
- Keep Preview and Stable metadata separate; never redirect Stable users to Preview implicitly.
- Ensure public metadata and downloads remain accessible without a Toren account.

## 9. Publish and verify

Run `Publish Release` again with `dry_run=false`, the same tag/package/signed-macOS run IDs, and the successful `validation_run_id`.

- Confirm the workflow re-promotes into an empty output directory.
- Confirm the Release Validation record is downloaded and accepted.
- Confirm publication is rejected if any final filename/hash differs from the validated dry-run artifact set.
- Create the GitHub release only from the immutable release tag.
- Mark Preview releases as prereleases; Stable releases must not be marked as prereleases and should become GitHub Latest.
- Attach only the final artifacts that match the validated release-candidate bytes.
- Attach their checksums and release notes.
- Verify every published download can be retrieved and its checksum matches the published value.
- Verify release metadata points to the tagged source commit.

## 10. After publication

- Perform one fresh-install/first-launch smoke test from the published artifacts where practical.
- Record any release-specific regression immediately in the MVP tracker or a dedicated issue.
- Do not overwrite published artifacts in place. Ship a new version/tag when distributed bytes must change.