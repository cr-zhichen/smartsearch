#!/usr/bin/env bash
set -euo pipefail
# Args: App, pinned Sparkle distribution, fresh output, private key file, architecture, optional baseline.
app="$1"; tools="$2"; output="$3"; key_file="$4"; architecture="$5"; previous="${6:-}"
[[ "$architecture" == arm64 || "$architecture" == x86_64 ]]
[[ -d "$app" && -f "$key_file" && ! -e "$output" ]]
version="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleVersion' "$app/Contents/Info.plist")"
[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]
public_key="$(/usr/libexec/PlistBuddy -c 'Print :SUPublicEDKey' "$app/Contents/Info.plist")"
derived_key="$(SPARKLE_KEY_FILE="$key_file" swift - <<'SWIFT'
import Foundation
import CryptoKit
let path = ProcessInfo.processInfo.environment["SPARKLE_KEY_FILE"]!
let value = try String(contentsOfFile: path).trimmingCharacters(in: .whitespacesAndNewlines)
guard let data = Data(base64Encoded: value), data.count == 32 else { fatalError("Expected an Ed25519 seed") }
print(try Curve25519.Signing.PrivateKey(rawRepresentation: data).publicKey.rawRepresentation.base64EncodedString())
SWIFT
)"
[[ "$derived_key" == "$public_key" ]] || { echo 'Sparkle key does not match the App public key.' >&2; exit 1; }
codesign --verify --deep --strict "$app"
mkdir "$output"
staging="$(mktemp -d "$(dirname "$output")/sparkle-stage.XXXXXX")"
archive="SmartSearch-$version-macos-$architecture-sparkle.zip"
ditto -c -k --sequesterRsrc --keepParent "$app" "$staging/$archive"
has_baseline=false
if [[ -n "$previous" ]]; then
  [[ -d "$previous" ]]
  shopt -s nullglob
  old=("$previous"/SmartSearch-*-macos-"$architecture"-sparkle.zip)
  [[ ${#old[@]} -eq 1 ]] || { echo 'Expected exactly one verified Sparkle baseline.' >&2; exit 1; }
  cp "${old[0]}" "$staging/"
  has_baseline=true
fi
prefix="https://github.com/konbakuyomu/smartsearch/releases/download/v$version/"
if [[ -n "${SMART_SEARCH_TEST_DOWNLOAD_PREFIX:-}" ]]; then
  [[ "$(/usr/libexec/PlistBuddy -c 'Print :SSUpdateTestBuild' "$app/Contents/Info.plist")" == true ]]
  [[ "$SMART_SEARCH_TEST_DOWNLOAD_PREFIX" == http://127.0.0.1:*/* ]]
  prefix="$SMART_SEARCH_TEST_DOWNLOAD_PREFIX"
fi
"$tools/bin/generate_appcast" --ed-key-file "$key_file" --download-url-prefix "$prefix" \
  --link 'https://github.com/konbakuyomu/smartsearch/releases' --versions "$version" \
  --maximum-versions 1 --maximum-deltas 1 "$staging"
feeds=("$staging"/*.xml)
[[ ${#feeds[@]} -eq 1 ]]
"$tools/bin/sign_update" --verify --ed-key-file "$key_file" "${feeds[0]}"
python3 - "$staging" "$output" "${feeds[0]}" "$version" "$architecture" "$has_baseline" "$prefix" <<'PY'
from pathlib import Path
import shutil, sys, xml.etree.ElementTree as ET
stage, output, feed = map(Path, sys.argv[1:4])
version, architecture, baseline, prefix = sys.argv[4:]
ns = 'http://www.andymatuschak.org/xml-namespaces/sparkle'
items = ET.parse(feed).findall('./channel/item')
current = [i for i in items if i.findtext(f'{{{ns}}}version') == version]
assert len(current) == 1, 'Missing current appcast item'
enclosures = list(current[0].iter('enclosure'))
assert enclosures and (baseline != 'true' or len(enclosures) > 1), 'Expected delta was not generated'
for e in enclosures:
    url = e.attrib['url']
    assert url.startswith(prefix) and e.attrib.get(f'{{{ns}}}edSignature'), 'Untrusted or unsigned enclosure'
    name = url[len(prefix):]
    assert name and '/' not in name and '\\' not in name and f'macos-{architecture}' in name
    assert (stage / name).stat().st_size == int(e.attrib['length'])
    shutil.copy2(stage / name, output / name)
shutil.copy2(feed, output / f'appcast-macos-{architecture}.xml')
PY
echo "Sparkle $version $architecture packaged; delta baseline=$has_baseline"
