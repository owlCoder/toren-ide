# macOS signing and notarization

Public Toren IDE macOS preview and stable packages must not rely on users bypassing Gatekeeper.

Release packaging must provide:

- a stable application bundle identifier;
- Developer ID Application signing for direct distribution;
- hardened runtime configuration required by Apple's notarization workflow;
- notarization with Apple's notary service;
- stapling/verification of the notarization ticket where applicable;
- CI/release verification of the final distributed artifact;
- separate Apple Silicon and Intel artifacts where both architectures are supported, or a verified universal package.

Local source builds remain a developer workflow and do not require a Toren account or Toren cloud service.
