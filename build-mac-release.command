#!/bin/bash
set -e
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"
chmod +x build-mac-release.sh
./build-mac-release.sh

echo
echo "Trykk Enter for å lukke vinduet."
read -r
