#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"

VERSION="8.5.0"
FRAMEWORK="net10.0-maccatalyst"
RID="maccatalyst-arm64"
DIST="$(pwd)/dist"
STAGE="$(pwd)/.pkg-stage"

rm -rf "$DIST" "$STAGE"
mkdir -p "$DIST" "$STAGE"

echo "==> Restore"
dotnet restore

echo "==> Publish Release for Apple Silicon"
dotnet publish Stemplingsur.csproj -f "$FRAMEWORK" -c Release -r "$RID" --self-contained true

APP=$(find "bin/Release/$FRAMEWORK/$RID" -type d -name "*.app" -print -quit)
if [ -z "${APP:-}" ]; then
  echo "Fant ingen .app etter publish."
  exit 1
fi

OUT_APP="$DIST/timeKONTO-Stemplingsur-8.5.app"
cp -R "$APP" "$OUT_APP"

if [ -n "${APP_SIGN_IDENTITY:-}" ]; then
  echo "==> Signerer app med Developer ID"
  codesign --force --deep --options runtime --timestamp --sign "$APP_SIGN_IDENTITY" "$OUT_APP"
else
  echo "==> Ad-hoc signerer app for lokal testing"
  codesign --force --deep --sign - "$OUT_APP" || true
fi

cp -R "$OUT_APP" "$STAGE/timeKONTO-Stemplingsur-8.5.app"

PKG="$DIST/timeKONTO-Stemplingsur-8.5.pkg"
echo "==> Lager installerpakke"
if [ -n "${PKG_SIGN_IDENTITY:-}" ]; then
  pkgbuild \
    --root "$STAGE" \
    --install-location /Applications \
    --identifier bar.pts.stemplingsur \
    --version "$VERSION" \
    --sign "$PKG_SIGN_IDENTITY" \
    "$PKG"
else
  pkgbuild \
    --root "$STAGE" \
    --install-location /Applications \
    --identifier bar.pts.stemplingsur \
    --version "$VERSION" \
    "$PKG"
fi

rm -rf "$STAGE"

echo
echo "Ferdig:"
echo "  $OUT_APP"
echo "  $PKG"
