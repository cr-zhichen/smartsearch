#!/usr/bin/env python3
"""Use Sparkle's own command-line updater against isolated copies of the built App."""
from __future__ import annotations

import argparse
import functools
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
import json
import os
from pathlib import Path
import plistlib
import subprocess
import tempfile
import threading
import uuid

ROOT = Path(__file__).resolve().parents[2]
SPARKLE_COMMIT = "ac2def288cbff5cfc7df3ffef6abdf45b72bcb0a"  # Sparkle 2.9.6


def run(argv, log, *, env=None, timeout=600, check=True):
    result = subprocess.run([str(a) for a in argv], capture_output=True, text=True, env=env,
                            encoding="utf-8", errors="replace", timeout=timeout)
    log.write_text(result.stdout + result.stderr, encoding="utf-8")
    if check and result.returncode:
        raise RuntimeError(f"Sparkle check failed ({result.returncode}); see {log}")
    return result


def main(build_file):
    build = json.loads(build_file.read_text())
    root = Path(tempfile.mkdtemp(prefix="ss-sparkle-"))
    evidence = ROOT / ".desktop-artifacts" / ("sparkle-check-" + uuid.uuid4().hex)
    evidence.mkdir()
    key = root / "test-key"
    public = root / "test-public-key"
    wrong_public = root / "wrong-public-key"
    env = {**os.environ, "SS_TEST_KEY_FILE": str(key), "SS_TEST_PUBLIC_FILE": str(public), "SS_WRONG_PUBLIC_FILE": str(wrong_public)}
    generator = root / "key.swift"
    generator.write_text('''import Foundation
import CryptoKit
let key = Curve25519.Signing.PrivateKey()
let env = ProcessInfo.processInfo.environment
let path = env["SS_TEST_KEY_FILE"]!
try key.rawRepresentation.base64EncodedString().write(toFile: path, atomically: true, encoding: .utf8)
try FileManager.default.setAttributes([.posixPermissions: 0o600], ofItemAtPath: path)
try key.publicKey.rawRepresentation.base64EncodedString().write(toFile: env["SS_TEST_PUBLIC_FILE"]!, atomically: true, encoding: .utf8)
try Curve25519.Signing.PrivateKey().publicKey.rawRepresentation.base64EncodedString().write(toFile: env["SS_WRONG_PUBLIC_FILE"]!, atomically: true, encoding: .utf8)
''')
    run(["swift", generator], evidence / "test-key.log", env=env)
    server = None
    try:
        source = root / "sparkle-source"
        run(["git", "clone", "--depth", "1", "--branch", "2.9.6", "https://github.com/sparkle-project/Sparkle", source], evidence / "tool-source.log")
        commit = subprocess.check_output(["git", "-C", str(source), "rev-parse", "HEAD"], text=True).strip()
        assert commit == SPARKLE_COMMIT, "Unexpected Sparkle CLI source"
        derived = root / "tool-build"
        run(["xcodebuild", "-project", source / "Sparkle.xcodeproj", "-scheme", "sparkle-cli", "-configuration", "Release",
             "-derivedDataPath", derived, "CODE_SIGN_IDENTITY=-", "CODE_SIGNING_ALLOWED=YES"], evidence / "tool-build.log")
        tool_app = derived / "Build/Products/Release/sparkle.app"
        tool_plist = tool_app / "Contents/Info.plist"
        tool_info = plistlib.loads(tool_plist.read_bytes())
        tool_info["CFBundleIdentifier"] = "com.smartsearch.test.helper." + uuid.uuid4().hex
        tool_info["NSAppTransportSecurity"] = {"NSAllowsLocalNetworking": True}
        tool_plist.write_bytes(plistlib.dumps(tool_info))
        run(["codesign", "--force", "--sign", "-", tool_app], evidence / "tool-sign.log")
        cli = tool_app / "Contents/MacOS/sparkle"
        actual_app = Path(build["app"])
        actual = plistlib.loads((actual_app / "Contents/Info.plist").read_bytes())
        version = actual["CFBundleVersion"]
        architecture = build["architecture"]
        current = {"directory": root, "case": ""}
        requests = []

        class Handler(SimpleHTTPRequestHandler):
            def log_message(self, *args):
                pass

            def do_GET(self):
                requests.append(self.path)
                self.directory = str(current["directory"])
                corrupt = current["case"] == "full-fallback" and self.path.endswith(".delta")
                if corrupt:
                    data = bytearray((current["directory"] / self.path.removeprefix("/")).read_bytes())
                    data[-1] ^= 1
                    self.send_response(200)
                    self.send_header("Content-Length", str(len(data)))
                    self.end_headers()
                    self.wfile.write(data)
                else:
                    super().do_GET()

        server = ThreadingHTTPServer(("127.0.0.1", 0), functools.partial(Handler, directory=str(root)))
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        prefix = f"http://127.0.0.1:{server.server_port}/"
        feed_url = prefix + f"appcast-macos-{architecture}.xml"
        receipts = []
        for case in ("delta", "full-fallback", "wrong-public-key"):
            case_root = root / case
            case_root.mkdir()
            bundle_id = "com.smartsearch.test." + uuid.uuid4().hex
            apps = []
            for label, app_version in (("old", "0.0.1"), ("new", version)):
                app = case_root / label / "Smart Search.app"
                app.parent.mkdir()
                run(["ditto", actual_app, app], evidence / f"{case}-{label}-copy.log")
                info = {**actual, "CFBundleIdentifier": bundle_id, "CFBundleVersion": app_version,
                        "CFBundleShortVersionString": app_version, "SUPublicEDKey": public.read_text(),
                        "SUFeedURL": feed_url, "SSUpdateTestBuild": True,
                        "NSAppTransportSecurity": {"NSAllowsLocalNetworking": True}}
                (app / "Contents/Info.plist").write_bytes(plistlib.dumps(info))
                backend = app / "Contents/Resources/backend/package.json"
                package = json.loads(backend.read_text())
                backend.write_text(json.dumps({**package, "version": app_version}))
                run(["codesign", "--force", "--sign", "-", app], evidence / f"{case}-{label}-sign.log")
                previous = case_root / "old-feed" if label == "new" else ""
                run(["bash", ROOT / "desktop/scripts/package-sparkle.sh", app, build["sparkle_tools"],
                     case_root / (label + "-feed"), key, architecture, previous], evidence / f"{case}-{label}-package.log",
                    env={**os.environ, "SMART_SEARCH_TEST_DOWNLOAD_PREFIX": prefix})
                apps.append(app)
            if case == "wrong-public-key":
                info = plistlib.loads((apps[0] / "Contents/Info.plist").read_bytes())
                info["SUPublicEDKey"] = wrong_public.read_text()
                (apps[0] / "Contents/Info.plist").write_bytes(plistlib.dumps(info))
                run(["codesign", "--force", "--sign", "-", apps[0]], evidence / f"{case}-resign.log")
            current.update(directory=case_root / "new-feed", case=case)
            requests.clear()
            result = run([cli, apps[0], "--check-immediately", "--feed-url", feed_url, "--user-agent-name", "Smart Search isolated update test", "--verbose"],
                         evidence / f"{case}-update.log", timeout=180, check=False)
            installed = plistlib.loads((apps[0] / "Contents/Info.plist").read_bytes())["CFBundleVersion"]
            if case == "wrong-public-key":
                assert result.returncode != 0 and installed == "0.0.1", "Untrusted update was applied"
                assert "signatur" in (result.stdout + result.stderr).lower(), "Failure was not a signature rejection"
            else:
                assert result.returncode == 0 and installed == version, f"{case}: update failed"
                assert any(p.endswith(".delta") for p in requests), requests
                assert any(p.endswith(".zip") for p in requests) == (case == "full-fallback"), requests
                backend = apps[0] / "Contents/Resources/backend/smart-search"
                check = run([backend, "--version"], evidence / f"{case}-backend.log")
                assert check.stdout.strip() == "smart-search " + version
            receipts.append({"case": case, "requests": list(requests), "version": installed, "exit_code": result.returncode})
        (evidence / "receipt.json").write_text(json.dumps({"status": "passed", "architecture": architecture, "root": str(root),
            "build": str(build_file), "cases": receipts, "gui_tested": False, "production_install_modified": False}, indent=2))
        print(evidence / "receipt.json")
    finally:
        if server:
            server.shutdown()
            server.server_close()
            thread.join(5)
        key.unlink(missing_ok=True)  # Only this exact ephemeral private key is removed.


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("build", type=Path)
    main(parser.parse_args().build.resolve())
