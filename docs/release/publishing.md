# Publishing Preview and Stable releases

Toren IDE separates package validation, platform signing, and public release publication. The final GitHub release is never created directly from an unsigned package workflow run.

## Workflows

1. `Package` builds the four release-candidate RIDs, verifies each SHA-256 checksum, and emits `Toren-IDE-release-manifest.json` as candidate metadata.
2. `Signed macOS Package` signs, notarizes, staples, and verifies one macOS RID. Its artifact also contains a signed-artifact attestation with the exact source commit, release version, RID, and SHA-256 hash.
3. `Publish Release` promotes validated artifacts and optionally creates the GitHub release. It is manual-only and defaults to dry-run mode.

The release checklist in `checklist.md` remains authoritative for the human signing, smoke-test, accessibility, and final release gates.

## Preparing a release

Create the immutable release tag first:

- Preview: `vMAJOR.MINOR.PATCH-preview.N`
- Stable: `vMAJOR.MINOR.PATCH`

Run `Package` for the tagged source commit/version. Record its workflow run ID after all four RID jobs and the `release-candidate-manifest` job are green.

Run `Signed macOS Package` twice for the same source ref and release version:

- once for `osx-x64`;
- once for `osx-arm64`.

Record both successful workflow run IDs. The signed artifacts must contain the ZIP, its SHA-256 file, and `Toren IDE.release.json`.

Complete the platform smoke/accessibility gates from `checklist.md` before promotion.

## Dry-run promotion

Start `Publish Release` with:

- `release_tag` — the immutable Preview or Stable tag;
- `package_run_id` — the successful `Package` run;
- `macos_x64_run_id` — the successful signed Intel macOS run;
- `macos_arm64_run_id` — the successful signed Apple Silicon macOS run;
- `confirm_release_gates` — `true` only after the required platform gates have actually been completed;
- `dry_run` — leave `true` for the first promotion attempt.

The workflow verifies that:

- the tag format determines the expected version and channel;
- the checked-out tag resolves to one exact commit;
- the candidate manifest version, channel, and source commit match the tag;
- candidate Windows/Linux package bytes match their checksums and manifest hashes;
- both signed macOS attestations match the exact release version, RID, source commit, and ZIP hash;
- the macOS attestations confirm Developer ID signing, notarization, and stapling.

A successful dry run uploads `Toren-IDE-release-promotion` containing the final normalized package names, fresh checksums, and the promoted distribution manifest. Review this artifact before public publication.

## Publishing

Run the same `Publish Release` workflow again with the same validated run IDs and `dry_run=false`.

The workflow refuses to overwrite an existing GitHub release. Preview tags are published as prereleases; Stable tags are published as normal releases. Final release artifacts, checksums, and the promoted release manifest are attached together.

If any distributed bytes must change after publication, create a new version and tag. Do not replace already-published artifacts in place.

## Security and integrity boundary

`confirm_release_gates=true` is an operator assertion, not a substitute for automated verification. It is intentionally required because CI cannot prove hands-on platform smoke tests or visual/accessibility review.

The promotion workflow minimizes accidental cross-release mixing by binding package metadata and signed macOS attestations to the same immutable source commit and release version. A candidate manifest alone never marks an artifact as distribution-ready.
