#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 3 ]]; then
  echo "usage: $0 <app-bundle> <developer-id-identity> <notary-keychain-profile>" >&2
  exit 64
fi

APP_BUNDLE="$1"
SIGNING_IDENTITY="$2"
NOTARY_PROFILE="$3"

if [[ ! -d "$APP_BUNDLE" ]]; then
  echo "App bundle not found: $APP_BUNDLE" >&2
  exit 66
fi

MAIN_EXECUTABLE="$APP_BUNDLE/Contents/MacOS/Toren.App"
if [[ ! -x "$MAIN_EXECUTABLE" ]]; then
  echo "Main executable not found or not executable: $MAIN_EXECUTABLE" >&2
  exit 66
fi

# Sign nested Mach-O code first. Do not use --deep for signing: every nested binary
# gets an explicit Developer ID signature, hardened runtime and secure timestamp.
while IFS= read -r -d '' candidate; do
  if [[ "$candidate" == "$MAIN_EXECUTABLE" ]]; then
    continue
  fi

  if file "$candidate" | grep -q "Mach-O"; then
    codesign \
      --force \
      --options runtime \
      --timestamp \
      --sign "$SIGNING_IDENTITY" \
      "$candidate"
  fi
done < <(find "$APP_BUNDLE/Contents" -type f -print0)

codesign \
  --force \
  --options runtime \
  --timestamp \
  --sign "$SIGNING_IDENTITY" \
  "$MAIN_EXECUTABLE"

codesign \
  --force \
  --options runtime \
  --timestamp \
  --sign "$SIGNING_IDENTITY" \
  "$APP_BUNDLE"

codesign --verify --deep --strict --verbose=2 "$APP_BUNDLE"

NOTARY_ARCHIVE="${APP_BUNDLE%.app}-notary.zip"
rm -f "$NOTARY_ARCHIVE"
ditto -c -k --sequesterRsrc --keepParent "$APP_BUNDLE" "$NOTARY_ARCHIVE"

xcrun notarytool submit "$NOTARY_ARCHIVE" \
  --keychain-profile "$NOTARY_PROFILE" \
  --wait

xcrun stapler staple "$APP_BUNDLE"
xcrun stapler validate "$APP_BUNDLE"
codesign --verify --deep --strict --verbose=2 "$APP_BUNDLE"
spctl --assess --type execute --verbose=2 "$APP_BUNDLE"

FINAL_ARCHIVE="${APP_BUNDLE%.app}.zip"
rm -f "$FINAL_ARCHIVE"
ditto -c -k --sequesterRsrc --keepParent "$APP_BUNDLE" "$FINAL_ARCHIVE"

printf '%s\n' "$FINAL_ARCHIVE"
