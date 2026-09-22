#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd -P)"
app_directory="$1"; sparkle_tools="$2"; architecture="$3"; python_bin="$4"
update_key_file="${5:-}"; previous_release_directory="${6:-}"
run_directory="$(dirname "$app_directory")"
version="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleVersion' "$app_directory/Contents/Info.plist")"
suffix="-$architecture"
verify_args=(--architecture "$architecture" --version "$version")
if [[ "$architecture" == universal ]]; then
  suffix=""
else
  verify_args+=(--sdk-version "$(xcrun --sdk macosx --show-sdk-version)")
fi

# Seal every resource after final assembly. This is ad-hoc signing, not notarization.
codesign --force --deep --sign - "$app_directory"
codesign --verify --deep --strict --verbose=2 "$app_directory"
bash "$repository_root/desktop/scripts/check-macos-backend.sh" "$app_directory/Contents/Resources/backend/smart-search"
dmg="$run_directory/SmartSearch-v$version$suffix.dmg"
"${DMGBUILD:-dmgbuild}" -s "$repository_root/desktop/packaging/macos/dmg-settings.py" \
  -D "app=$app_directory" -D "assets=$repository_root/desktop/packaging/macos" "Smart Search" "$dmg"
"$python_bin" "$repository_root/desktop/scripts/verify_macos_dmg.py" "$dmg" \
  "${verify_args[@]}"

updates_directory=""
if [[ -n "$update_key_file" ]]; then
  updates_directory="$run_directory/updates"
  bash "$repository_root/desktop/scripts/package-sparkle.sh" "$app_directory" "$sparkle_tools" \
    "$updates_directory" "$update_key_file" "$architecture" "$previous_release_directory"
  cp "$dmg" "$updates_directory/"
fi
"$python_bin" - "$run_directory/result.json" "$app_directory" "$dmg" "$sparkle_tools" "$updates_directory" "$architecture" <<'PY'
import json, sys
from pathlib import Path
result, app, dmg, tools, updates, architecture = sys.argv[1:]
Path(result).write_text(json.dumps(dict(app=app, dmg=dmg, sparkle_tools=tools, updates_directory=updates,
    architecture=architecture, code_signing='ad-hoc-test', notarized=False), indent=2))
PY
echo "macOS package (ad-hoc signed, not notarized): $dmg"
