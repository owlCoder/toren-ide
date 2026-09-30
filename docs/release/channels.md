# Release channels

Toren IDE uses two public release channels for MVP 1.0 delivery.

## Preview

Preview releases are intended for early adopters and validation. Tag format:

`vMAJOR.MINOR.PATCH-preview.N`

Preview builds may ship more frequently, but they still require green CI, successful package generation, checksums, and the same required platform signing/notarization policy as Stable builds when distributed publicly. For MVP 1.0, mandatory platform signing/notarization applies to macOS Developer ID distribution; Windows Authenticode signing is not an MVP release gate.

## Stable

Stable releases use tags in the form:

`vMAJOR.MINOR.PATCH`

A stable release requires all MVP release gates to be complete: green cross-platform CI, verified packages, required macOS signing/notarization, platform smoke tests, release notes, checksums, and final visual/accessibility validation. Windows ZIP artifacts remain unsigned for MVP 1.0 unless the release policy is explicitly changed together with a signing workflow and validation contract.

## Update policy

Toren IDE remains local-first and account-free. Release metadata must be retrievable without requiring a Toren account. The in-app updater is not considered complete merely because package artifacts exist; until update delivery and verification are implemented, users obtain new builds from the published release artifacts.

When automatic update delivery is introduced, it must keep Preview and Stable feeds separate, verify downloaded artifacts before installation, and allow users to remain on Stable without being moved to Preview implicitly.
