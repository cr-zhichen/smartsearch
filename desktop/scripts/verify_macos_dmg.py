#!/usr/bin/env python3
"""Mount the actual DMG read-only and verify the copied app, then unmount it."""

from __future__ import annotations

import argparse
import os
import plistlib
import subprocess
import tempfile
from pathlib import Path

from build_backend import smoke_backend


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("dmg", type=Path)
    parser.add_argument("--architecture", required=True, choices=("arm64", "x86_64"))
    parser.add_argument("--version", required=True)
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
                subprocess.run(["lipo", "-verify_arch", args.architecture, str(executable)], check=True)
            # Copy exactly what a Finder install copies, including resource seals.
            installed = root / "Applications/Smart Search.app"
            subprocess.run(["ditto", str(app), str(installed)], check=True)
            subprocess.run(["codesign", "--verify", "--deep", "--strict", "--verbose=2", str(installed)], check=True)
            smoke_backend(installed / "Contents/Resources/backend/smart-search", root, args.version)
            print("DMG verified: signature, version, architecture, install layout and installed backend startup.")
            print("Ad-hoc signatures do not establish Developer ID trust or Apple notarization.")
        finally:
            subprocess.run(["hdiutil", "detach", str(mount)], check=True)


if __name__ == "__main__":
    main()
