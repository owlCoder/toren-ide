# Toren IDE release checklist

Use this checklist for every public Preview or Stable release. It complements `packaging.md`, `macos-signing.md`, `channels.md`, and `publishing.md`; it does not replace platform-specific signing or hands-on smoke testing.

## 1. Select the channel and version

- Preview tag: `vMAJOR.MINOR.PATCH-preview.N`.
- Stable tag: `vMAJOR.MINOR.PATCH`.
- Confirm the release commit is the exact commit intended for distribution.
- Do not move an existing public release tag to a different commit.

Before spending signing credentials, run the local release preflight against the intended commit. Before the immutable tag exists, use `-AllowMissingTag`; after creating the tag, rerun without it:

```powershell
./scripts/assert-release-preflight.ps1 -ReleaseVersion <version> -ExpectedCommit <40-char-sha> -AllowMissingTag
./scripts/assert-release-preflight.ps1 -ReleaseVersion <version> -ExpectedCommit <40-char-sha>
```

The preflight rejects unsupported version syntax, the wrong checked-out commit, a missing/mispointed release tag, or missing release workflow/script/documentation files.

## 2. Validate source and CI

- Confirm the normal CI workflow is green on Linux, Windows, and macOS.
- Confirm NUnit, xUnit, MSTest, and Microsoft Testing Platform compatibility gates are green.
- Confirm the release-promotion, release-validation, workflow-run provenance, and release-preflight contract tests are green on all three CI platforms.
- Review open release-blocking issues and known regressions.
- Confirm `docs/progress.md` does not claim a release gate that has not actually been validated.

## 3. Build release candidates

Run the `Package` workflow for the release ref/version and confirm all candidate artifacts are present:

- `linux-x64`;
- `win-x64`;
- `osx-x64`;
- `osx-arm64`.

The workflow also produces `Toren-IDE-release-manifest.json`. Treat this file as **candidate metadata**, not as a public update feed. It records the release version/channel, source commit, runtime identifiers, package filenames, and verified SHA-256 hashes. Candidate entries remain `distributionReady=false` until promotion has completed the required provenance/signing checks.

Verify that every package checksum in the manifest matches the packaged artifact. The MVP signing policy is explicit: `requiresPlatformSigning=true` for both macOS RIDs and `false` for Windows/Linux. A checksum mismatch, duplicate/missing RID, invalid signing policy, or pre-promoted `distributionReady=true` entry is a release blocker.

## 4. Produce signed platform artifacts

### macOS

- Run the signed macOS workflow for both `osx-arm64` and `osx-x64` when both architectures are being distributed.
- Use the exact immutable `v<release_version>` tag as `source_ref`; the workflow must reject `main` or any other mutable/different ref.
- Verify Developer ID signing and hardened runtime.
- Submit for notarization and require an accepted result.
- Staple the notarization ticket.
- Run `codesign --verify --deep --strict` and `spctl --assess --type execute` on the final `.app`.
- Confirm the signed-artifact attestation matches the exact release version, source commit, RID and ZIP SHA-256.

### Windows

- Windows Authenticode/code signing is not an MVP 1.0 release gate; `win-x64` must remain `requiresPlatformSigning=false` unless the release policy, signing workflow, attestation and promotion validation are changed together.
- Verify the final ZIP contains the expected self-contained executable and dependencies.

### Linux

- Verify the final archive preserves the executable bit on `Toren.App`.

## 5. Assemble the dry-run promotion

Run `Publish Release` with `dry_run=true` using the exact Package run plus both signed macOS runs for the immutable release tag.

- Confirm the workflow produces `Toren-IDE-release-promotion`.
- Confirm the promoted set contains exactly four normalized package artifacts, four fresh SHA-256 files, and `Toren-IDE-release-manifest.json` — nine files total.
- Confirm the promoted manifest is bound to the expected release version/channel/source commit.
- Confirm the promoted manifest records the producing `Publish Release` workflow run ID/attempt and `promotionDryRun=true`.
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
- the exact dry-run promotion workflow run ID and attempt;
- the Release Validation workflow run ID and attempt;
- the validating GitHub actor and timestamp;
- every required check as true;
- exactly one package artifact for `linux-x64`, `win-x64`, `osx-x64`, and `osx-arm64`;
- the exact filename and SHA-256 for every promoted package;
- the exact promoted manifest SHA-256;
- exactly nine hashed promotion assets: four packages, four checksum files, and the promoted manifest.

The validation workflow re-hashes every promoted package/checksum/manifest byte and rejects an unexpected or missing promotion file. Do not hand-edit or reuse a validation record for a different tag, commit, promotion run, validation run, or asset byte set.

## 8. Prepare release notes and final metadata

- Summarize user-visible changes since the previous public release.
- List known issues that are not release blockers.
- Publish SHA-256 checksums beside the **final distributed artifacts**, not only the unsigned CI candidates.
- Keep Preview and Stable metadata separate; never redirect Stable users to Preview implicitly.
- Ensure public metadata and downloads remain accessible without a Toren account.

## 9. Publish and verify

Run `Publish Release` again with `dry_run=false`, the same immutable tag, and the successful `validation_run_id`. Package and signed-macOS run IDs are dry-run-only inputs and are not used for public publication.

- Confirm the Release Validation record is downloaded and accepted.
- Confirm the workflow resolves the exact dry-run `promotionRunId` from that record.
- Confirm the workflow downloads the already-tested `Toren-IDE-release-promotion` artifact instead of re-promoting source artifacts.
- Confirm publication is rejected if the promoted manifest changes, any package/checksum byte changes, or any file is added/removed from the validated nine-file asset set.
- Confirm the validation record's own workflow run ID matches the supplied `validation_run_id`.
- Create the GitHub release only from the immutable release tag.
- Mark Preview releases as prereleases; Stable releases must not be marked as prereleases and should become GitHub Latest.
- Attach only the exact files from the hands-on-validated dry-run promotion artifact.
- Verify every published download can be retrieved and its checksum matches the published value.
- Verify release metadata points to the tagged source commit.

## 10. After publication

- Perform one fresh-install/first-launch smoke test from the published artifacts where practical.
- Record any release-specific regression immediately in the MVP tracker or a dedicated issue.
- Do not overwrite published artifacts in place. Ship a new version/tag when distributed bytes must change.
