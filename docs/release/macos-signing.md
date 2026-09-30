# macOS signing and notarization

Public Toren IDE macOS preview and stable packages must not rely on users bypassing Gatekeeper.

The repository contains two distinct paths:

- `Package` produces unsigned CI validation bundles for `osx-x64` and `osx-arm64`;
- `Signed macOS Package` is a manual release gate that requires real Apple release credentials, signs the bundle, submits it to Apple's notary service, staples the ticket and verifies the final app before upload.

## Required GitHub Actions secrets

The signed release workflow intentionally fails before checkout if any required release secret is missing:

- `APPLE_DEVELOPER_ID_P12` — base64-encoded Developer ID Application certificate and private key exported as PKCS#12;
- `APPLE_DEVELOPER_ID_P12_PASSWORD` — password protecting that PKCS#12 file;
- `APPLE_DEVELOPER_ID_IDENTITY` — exact `Developer ID Application: ...` signing identity;
- `APPLE_NOTARY_KEY_ID` — App Store Connect API key ID;
- `APPLE_NOTARY_ISSUER_ID` — App Store Connect issuer ID;
- `APPLE_NOTARY_PRIVATE_KEY` — contents of the matching `.p8` private key.

None of these values belong in the repository.

## Signing behavior

`scripts/sign-notarize-macos.sh` signs nested Mach-O files explicitly before signing the main executable and app bundle. Signing does not use `codesign --deep`; nested code receives an explicit signature. The release signature enables hardened runtime and requests a secure timestamp.

The workflow then:

1. verifies the Developer ID signature;
2. creates a notarization ZIP with `ditto`;
3. submits it through `xcrun notarytool ... --wait` using a temporary keychain profile;
4. staples and validates the notarization ticket;
5. re-verifies the code signature;
6. runs Gatekeeper assessment with `spctl`;
7. creates the final ZIP and SHA-256 checksum.

Apple requires Developer ID signing, hardened runtime and valid signatures before notarization. `notarytool` is the supported command-line notarization path; the old `altool` path is intentionally not used.

## Release validation

A green unsigned `Package` workflow is not evidence of notarization. A public macOS artifact should be treated as release-ready only after `Signed macOS Package` completes successfully for that exact source ref and RID, followed by a smoke test on a clean target Mac.

Both Apple Silicon and Intel packages remain separate until a universal package is intentionally introduced and tested.

Local source builds remain a developer workflow and do not require a Toren account or Toren cloud service.
