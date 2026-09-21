#!/usr/bin/env python3
"""Mount the actual DMG read-only and verify the copied app, then unmount it."""

from __future__ import annotations

import argparse
import os
import plistlib
import re
import subprocess
import tempfile
from pathlib import Path

from build_backend import smoke_backend


def verify_frontend_sdk(executable: Path, architecture: str, minimum_os: str, expected_sdk: str | None = None) -> None:
    """Check the real Mach-O linked-on SDK, not the compiler or plist version."""
    output = subprocess.run(
        ["xcrun", "vtool", "-arch", architecture, "-show-build", str(executable)],
        check=True, capture_output=True, text=True,
    ).stdout
    versions = dict(re.findall(r"^\s*(minos|sdk)\s+(\d+(?:\.\d+)*)\s*$", output, re.MULTILINE))

    def version(value: str) -> tuple[int, ...]:
        return (tuple(int(part) for part in value.split(".")) + (0, 0))[:3]

    if "sdk" not in versions or version(versions["sdk"]) < (26, 0, 0):
        raise RuntimeError(f"Frontend linked-on SDK is {versions.get('sdk', 'missing')}; SDK 26+ is required for the new native design")
    if expected_sdk is not None and version(versions["sdk"]) != version(expected_sdk):
        raise RuntimeError(f"Frontend records SDK {versions['sdk']} but was built with SDK {expected_sdk}")
    if "minos" not in versions or version(versions["minos"]) != version(minimum_os):
        raise RuntimeError(f"Frontend deployment target differs from Info.plist minimum {minimum_os}")
    print(f"Frontend verified: linked-on SDK {versions['sdk']}, minimum macOS {versions['minos']}.")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("dmg", type=Path)
    parser.add_argument("--architecture", required=True, choices=("arm64", "x86_64"))
    parser.add_argument("--version", required=True)
    parser.add_argument("--sdk-version", help="Expected SDK used by the build toolchain")
    args = parser.parse_args()

    with tempfile.TemporaryDirectory(prefix="smartsearch-dmg-") as directory:
        root = Path(directory)
        mount = root / "volume"
        mount.mkdir()
        subprocess.run(
            ["hdiutil", "attach", "-readonly", "-nobrowse", "-mountpoint", str(mount), str(args.dmg.resolve())],
            check=True,
        )
        try:
            app = mount / "Smart Search.app"
            subprocess.run(["codesign", "--verify", "--deep", "--strict", "--verbose=2", str(app)], check=True)
            info = plistlib.loads((app / "Contents/Info.plist").read_bytes())
            if info["CFBundleShortVersionString"] != args.version or info["CFBundleVersion"] != args.version:
                raise RuntimeError("The packaged app version differs from the build source")
            if not (mount / "Applications").is_symlink() or os.readlink(mount / "Applications") != "/Applications":
                raise RuntimeError("The DMG is missing its Applications installation link")
            if not (mount / ".DS_Store").is_file() or not (mount / ".background.tiff").is_file():
                raise RuntimeError("The DMG is missing its Finder layout or Retina background")
            desktop = app / "Contents/MacOS/SmartSearchDesktop"
            backend = app / "Contents/Resources/backend/smart-search"
            for executable in (desktop, backend):
                subprocess.run(["xcrun", "lipo", str(executable), "-verify_arch", args.architecture], check=True)
            verify_frontend_sdk(desktop, args.architecture, info["LSMinimumSystemVersion"], args.sdk_version)
            # Copy exactly what a Finder install copies, including resource seals.
            installed = root / "Applications/Smart Search.app"
            subprocess.run(["ditto", str(app), str(installed)], check=True)
            subprocess.run(["codesign", "--verify", "--deep", "--strict", "--verbose=2", str(installed)], check=True)
            smoke_backend(installed / "Contents/Resources/backend/smart-search", root, args.version)
            print("DMG verified: signature, version, architecture, frontend SDK, install layout and installed backend startup.")
            print("Ad-hoc signatures do not establish Developer ID trust or Apple notarization.")
        finally:
            subprocess.run(["hdiutil", "detach", str(mount)], check=True)


if __name__ == "__main__":
    main()
