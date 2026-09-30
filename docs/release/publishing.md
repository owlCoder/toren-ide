# Publishing Preview and Stable releases

Toren IDE separates package validation, platform signing, hands-on release validation, and public release publication. The final GitHub release is never created directly from an unsigned package workflow run or from unvalidated promoted bytes.

## Workflows

1. `Package` builds the four release-candidate RIDs, verifies each SHA-256 checksum, and emits `Toren-IDE-release-manifest.json` as candidate metadata.
2. `Signed macOS Package` signs, notarizes, staples, and verifies one macOS RID. Its artifact also contains a signed-artifact attestation with the exact source commit, release version, RID, and SHA-256 hash.
3. `Publish Release` promotes validated package inputs. It is manual-only and defaults to dry-run mode; the dry run emits the exact normalized artifact set intended for validation.
4. `Release Validation` consumes one successful dry-run promotion artifact after hands-on platform testing and records the validating operator, release identity, required smoke/accessibility/performance confirmations, and exact promoted artifact hashes.
5. `Publish Release` with `dry_run=false` requires a successful `Release Validation` run and re-verifies that the newly promoted bytes exactly match the validated dry-run bytes before creating the public GitHub release.

The release checklist in `checklist.md` remains authoritative for the human signing, smoke-test, accessibility, performance, and final release gates. The validation workflow records those completed gates; it does not automate or replace them.

## Preparing a release

Create the immutable release tag first:

- Preview: `vMAJOR.MINOR.PATCH-preview.N`
- Stable: `vMAJOR.MINOR.PATCH`

Run `Package` for the tagged source commit/version. Record its workflow run ID after all four RID jobs and the `release-candidate-manifest` job are green.

Run `Signed macOS Package` twice for the same source ref and release version:

- once for `osx-x64`;
- once for `osx-arm64`.

Record both successful workflow run IDs. The signed artifacts must contain the ZIP, its SHA-256 file, and `Toren IDE.release.json`.

## Dry-run promotion

Start `Publish Release` with:

- `release_tag` — the immutable Preview or Stable tag;
- `package_run_id` — the successful `Package` run;
- `macos_x64_run_id` — the successful signed Intel macOS run;
- `macos_arm64_run_id` — the successful signed Apple Silicon macOS run;
- `confirm_release_gates` — `true` only when the signing-related/operator prerequisites for assembling the candidate are satisfied;
- `dry_run` — leave `true` for the first promotion attempt;
- `validation_run_id` — leave empty during the dry run.

The workflow verifies that:

- the tag format determines the expected version and channel;
- the checked-out tag resolves to one exact commit;
- the candidate manifest uses the supported Toren schema/product and contains exactly one `linux-x64`, `win-x64`, `osx-x64`, and `osx-arm64` entry;
- candidate version, channel, source commit, package-validation state, signing policy, pre-promotion `distributionReady=false` state, filenames and SHA-256 values satisfy the release contract;
- candidate Windows/Linux package bytes match their checksums and manifest hashes;
- both signed macOS attestations match the supported schema/product plus exact release version, RID, source commit and ZIP hash;
- the macOS attestations confirm Developer ID signing, notarization, and stapling;
- the promotion output directory is empty before artifacts are assembled, preventing stale files from being included accidentally.

A successful dry run uploads `Toren-IDE-release-promotion` containing the exact normalized package names, fresh checksums, and promoted distribution manifest. This artifact — not the earlier unsigned candidate set — is what must receive the final hands-on release validation.

## Record hands-on release validation

Download or test the promoted dry-run candidates according to `checklist.md` on supported Windows, Linux, and macOS systems. Complete the required visual/accessibility and performance review against those same promoted bytes.

After all required checks pass, start `Release Validation` with:

- the same immutable `release_tag`;
- `promotion_run_id` — the successful `Publish Release` dry-run ID that produced the tested `Toren-IDE-release-promotion` artifact;
- all Windows/Linux/macOS smoke confirmations set to `true`;
- visual/accessibility confirmation set to `true`;
- performance confirmation set to `true`.

The workflow downloads the promoted artifact, re-verifies every package against its checksum and promoted manifest, and uploads `Toren-IDE-release-validation`. The record contains the exact version/channel/source commit, dry-run promotion ID, validating GitHub actor, validation timestamp, required check states, and exact package filenames/SHA-256 hashes.

If any required check is false, package bytes no longer match, the manifest identity is wrong, or macOS signing state is missing, the validation record is not produced.

## Publishing

Run `Publish Release` again with the same release tag and package/signed-macOS run IDs, set `dry_run=false`, and provide `validation_run_id` from the successful `Release Validation` workflow.

Before publication, the workflow promotes the inputs again into an empty destination, downloads the validation record, and rejects the release unless the new final package filename/hash set exactly matches the dry-run bytes that received hands-on validation. Any byte change therefore requires a new validation cycle.

The workflow also refuses to overwrite an existing GitHub release. Preview tags are published as prereleases and are never promoted to GitHub's Latest release. Stable tags are published as normal releases with `--latest`, making GitHub's standard `releases/latest` endpoint the account-free Stable channel pointer. Final release artifacts, checksums, and the promoted release manifest are attached together.

If any distributed bytes must change after publication, create a new version and tag. Do not replace already-published artifacts in place.

## Security and integrity boundary

`confirm_release_gates=true` remains an explicit operator assertion used by promotion, but public publication additionally requires a machine-readable Release Validation record bound to the exact dry-run artifact hashes. CI still cannot prove that a human actually inspected focus behavior, visual quality, launch behavior, or perceived performance; the record makes that operator assertion auditable and prevents publication of different bytes after validation.

The promotion workflow minimizes accidental cross-release mixing by binding package metadata and signed macOS attestations to the same immutable source commit and release version, validating the complete four-RID candidate contract, and refusing stale promotion output. A candidate manifest alone never marks an artifact as distribution-ready, and a dry-run promotion alone is insufficient for public publication.