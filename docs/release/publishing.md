# Publishing Preview and Stable releases

Toren IDE separates package validation, platform signing, hands-on release validation, and public release publication. The final GitHub release is never created directly from an unsigned package workflow run or from release bytes that differ from the hands-on-validated dry-run artifact set.

### Recorded exception: 1.0.0-preview.2

`1.0.0-preview.2` was published by maintainer decision as a Windows x64 and Linux x64 Preview directly from its Draft, without signed macOS packages, a dry-run promotion or a Release Validation record. Its release notes state this. The exception does not change the process below, which remains required for Stable releases and for any release that includes macOS packages.

## Workflows

1. `Package` builds the four release-candidate RIDs, verifies each SHA-256 checksum, and emits `Toren-IDE-release-manifest.json` as candidate metadata.
2. `Signed macOS Package` signs, notarizes, staples, and verifies one macOS RID. Its artifact also contains a signed-artifact attestation with the exact source commit, release version, RID, and SHA-256 hash.
3. `Publish Release` promotes validated package inputs. It is manual-only and defaults to dry-run mode; the dry run emits the exact normalized artifact set intended for validation and records its GitHub Actions run/attempt provenance in the promoted manifest.
4. `Release Validation` consumes one successful dry-run promotion artifact after hands-on platform testing and records the validating operator, release identity, validation workflow provenance, required smoke/accessibility/performance confirmations, the promoted manifest hash, and SHA-256 hashes for the complete promotion asset set.
5. `Publish Release` with `dry_run=false` requires a successful `Release Validation` record, resolves the exact dry-run promotion run from that record, downloads that already-tested `Toren-IDE-release-promotion` artifact, and re-verifies the complete asset set before creating the public GitHub release. Public publication does not re-promote package inputs.

The release checklist in `checklist.md` remains authoritative for the human signing, smoke-test, accessibility, performance, and final release gates. The validation workflow records those completed gates; it does not automate or replace them.

## Preparing a release

### Unpublished download draft

`Draft Release` provides a preparatory path before signing credentials and hands-on public-release gates are complete. It **only creates a GitHub Draft**; it cannot publish a public release or mark candidate manifest entries distribution-ready.

1. Add release notes at `docs/release/notes/<version>.md`, commit them with the release source, and create the immutable tag.
2. Wait for successful `CI` and `Package` runs on that exact commit. Tagged Package builds propagate the version into .NET assembly metadata, About, macOS bundle metadata and the candidate manifest; archives contain launch instructions, a license and release identity.
3. Run `Draft Release` with `release_tag`, `package_run_id` and `ci_run_id`.

The workflow checks successful same-repository source-run provenance, exact tag/commit/version, the four expected candidate filenames/signing policies, and all checksums. It attaches the four archives, four SHA-256 sidecars and candidate manifest to an **unpublished** draft. Draft assets are available to repository maintainers; they are not public downloads.

This path does not satisfy the signing or hands-on release gates below. Do not publish the candidate draft using GitHub's Publish button. After the signed/promoted artifact set has passed Release Validation, remove **only the unpublished draft**, keeping its immutable tag, then use the guarded `Publish Release` path. That workflow intentionally refuses to overwrite any existing release. Never delete or replace an already-public release to reuse a version.

### Signed public release

Create the immutable release tag first:

- Preview: `vMAJOR.MINOR.PATCH-preview.N`
- Stable: `vMAJOR.MINOR.PATCH`

Run `Package` for the tagged source commit/version. Record its workflow run ID after all four RID jobs and the `release-candidate-manifest` job are green.

Run `Signed macOS Package` twice for the same immutable release tag and release version:

- once for `osx-x64`;
- once for `osx-arm64`.

Record both successful workflow run IDs. The signed artifacts must contain the ZIP, its SHA-256 file, and `Toren IDE.release.json`.

## Dry-run promotion

Start `Publish Release` with:

- `release_tag` — the immutable Preview or Stable tag;
- `package_run_id` — the successful `Package` run;
- `macos_x64_run_id` — the successful signed Intel macOS run;
- `macos_arm64_run_id` — the successful signed Apple Silicon macOS run;
- `confirm_release_gates` — `true` only when the signing-related/operator prerequisites for assembling the candidate are satisfied; hands-on QA is recorded later by `Release Validation`;
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
- the promotion output directory is empty before artifacts are assembled, preventing stale files from being included accidentally;
- the promoted manifest records the producing `Publish Release` run ID, run attempt, and `promotionDryRun=true` provenance.

A successful dry run uploads `Toren-IDE-release-promotion` containing exactly four normalized packages, four fresh SHA-256 files, and the promoted distribution manifest. This nine-file artifact — not the earlier unsigned candidate set — is what must receive the final hands-on release validation.

## Record hands-on release validation

Download or test the promoted dry-run candidates according to `checklist.md` on supported Windows, Linux, and macOS systems. Complete the required visual/accessibility and performance review against those same promoted bytes.

After all required checks pass, start `Release Validation` with:

- the same immutable `release_tag`;
- `promotion_run_id` — the successful `Publish Release` dry-run ID that produced the tested `Toren-IDE-release-promotion` artifact;
- all Windows/Linux/macOS smoke confirmations set to `true`;
- visual/accessibility confirmation set to `true`;
- performance confirmation set to `true`.

The workflow downloads the promoted artifact and refuses to create a record unless the manifest says it came from that exact dry-run workflow run, every package still matches its checksum and promoted manifest, and the promotion set contains exactly the expected nine files. It then uploads `Toren-IDE-release-validation`.

The validation record contains the exact version/channel/source commit, dry-run promotion run ID and attempt, Release Validation run ID and attempt, validating GitHub actor, validation timestamp, required check states, exact package filenames/SHA-256 hashes, the promoted manifest SHA-256, and SHA-256 hashes for every file in the promotion artifact.

If any required check is false, package bytes no longer match, any checksum or manifest byte changes, the asset set gains/loses a file, the promotion provenance is wrong, the manifest identity is wrong, or macOS signing state is missing, the validation record is not produced or later validation fails.

## Publishing

Run `Publish Release` again with:

- the same `release_tag`;
- `dry_run=false`;
- `validation_run_id` from the successful `Release Validation` workflow;
- `confirm_release_gates=true` after the final checklist gates are complete.

`package_run_id`, `macos_x64_run_id`, and `macos_arm64_run_id` are not used for public publication. They are dry-run inputs only.

Before publication, the workflow downloads the validation record, resolves its recorded dry-run `promotionRunId`, downloads that exact `Toren-IDE-release-promotion` artifact, and rejects the release unless:

- the validation record belongs to the supplied Release Validation run ID;
- the release version/channel/source commit match the immutable tag;
- the promoted manifest bytes match the manifest hash captured during hands-on validation;
- the promoted manifest identifies the same dry-run workflow run and attempt;
- all four package filenames/hashes still match;
- every file in the nine-file promotion set has the exact SHA-256 captured by Release Validation;
- no promoted release file is missing or newly added.

There is no second promotion step on the public-publication path. The files attached to the GitHub release are the same bytes that were downloaded from the hands-on-validated dry-run artifact.

The workflow also refuses to overwrite an existing GitHub release. Preview tags are published as prereleases and are never promoted to GitHub's Latest release. Stable tags are published as normal releases with `--latest`, making GitHub's standard `releases/latest` endpoint the account-free Stable channel pointer. Final release artifacts, checksums, and the promoted release manifest are attached together.

If any distributed byte must change after validation, create a new dry-run promotion and repeat `Release Validation`. If distributed bytes must change after publication, create a new version and tag. Do not replace already-published artifacts in place.

## Security and integrity boundary

`confirm_release_gates=true` remains an explicit operator assertion used to authorize promotion/publication, but hands-on release acceptance is represented separately by the machine-readable Release Validation record. CI still cannot prove that a human actually inspected focus behavior, visual quality, launch behavior, or perceived performance; the record makes that operator assertion auditable.

The integrity boundary now covers the complete dry-run promotion asset set rather than only package payload hashes. Promotion provenance is recorded in the promoted manifest, Release Validation binds its own workflow provenance plus every promoted asset hash, and public publication downloads the exact recorded dry-run artifact instead of rebuilding equivalent output. Any package, checksum, manifest, file-set, release-identity, or workflow-provenance change invalidates the publication gate.

The promotion workflow also minimizes accidental cross-release mixing by binding package metadata and signed macOS attestations to the same immutable source commit and release version, validating the complete four-RID candidate contract, and refusing stale promotion output. A candidate manifest alone never marks an artifact as distribution-ready, and a dry-run promotion alone is insufficient for public publication.
