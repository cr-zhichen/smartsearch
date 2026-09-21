"""Release trust boundaries use synthetic packages, with no network or install."""
import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import xml.etree.ElementTree as ET

import pytest

spec = importlib.util.spec_from_file_location(
    "update_artifacts", Path(__file__).resolve().parents[1] / "desktop/scripts/update_artifacts.py"
)
updates = importlib.util.module_from_spec(spec)
spec.loader.exec_module(updates)
pytestmark = pytest.mark.skipif(sys.version_info < (3, 11), reason="Release runner uses Python 3.11+")
VERSION = "1.2.3"


def release_files(root):
    for arch in ("x64", "arm64"):
        package = root / f"com.smartsearch.desktop.win-{arch}-{VERSION}-full.nupkg"
        package.write_bytes(b"full " + arch.encode())
        (root / f"SmartSearch-{VERSION}-win-{arch}-Setup-signed.exe").write_bytes(b"installer")
        (root / f"releases.win-{arch}-stable.json").write_text(json.dumps({"Assets": [{
            "PackageId": f"com.smartsearch.desktop.win-{arch}", "Version": VERSION,
            "Type": "Full", "FileName": package.name, "Size": package.stat().st_size,
            "SHA256": hashlib.sha256(package.read_bytes()).hexdigest(),
        }]}))
    for arch in ("arm64", "x86_64"):
        package = root / f"SmartSearch-{VERSION}-macos-{arch}-sparkle.zip"
        package.write_bytes(b"mac full")
        (root / f"SmartSearch-{VERSION}-macos-{arch}-unsigned-test.dmg").write_bytes(b"dmg")
        rss = ET.Element("rss")
        item = ET.SubElement(ET.SubElement(rss, "channel"), "item")
        ET.SubElement(item, f"{{{updates.SPARKLE}}}version").text = VERSION
        ET.SubElement(item, "enclosure", {
            "url": f"https://github.com/{updates.REPOSITORY}/releases/download/v{VERSION}/{package.name}",
            "length": str(package.stat().st_size), f"{{{updates.SPARKLE}}}edSignature": "fixture-only",
        })
        ET.ElementTree(rss).write(root / f"appcast-macos-{arch}.xml")


@pytest.mark.parametrize("architecture", ["arm64", "x86_64"])
def test_sparkle_delta_names_are_architecture_specific_and_urls_are_decoded(tmp_path, monkeypatch, architecture):
    from urllib.parse import quote

    stage = tmp_path / "stage"
    output = tmp_path / "output"
    stage.mkdir()
    output.mkdir()
    release_files(stage)
    feed = stage / f"appcast-macos-{architecture}.xml"
    tree = ET.parse(feed)
    item = tree.find("./channel/item")
    prefix = f"https://github.com/{updates.REPOSITORY}/releases/download/v{VERSION}/"
    delta = stage / f"Smart Search{VERSION}-1.0.0.delta"
    delta.write_bytes(b"signed delta bytes")
    ET.SubElement(ET.SubElement(item, f"{{{updates.SPARKLE}}}deltas"), "enclosure", {
        "url": prefix + quote(delta.name), "length": str(delta.stat().st_size),
        f"{{{updates.SPARKLE}}}edSignature": "fixture-delta-signature",
        f"{{{updates.SPARKLE}}}deltaFrom": "1.0.0",
    })
    tree.write(feed)
    script = (Path(__file__).resolve().parents[1] / "desktop/scripts/package-sparkle.sh").read_text(encoding="utf-8")
    python = script.split("<<'PY'\n", 1)[1].split("\nPY", 1)[0]
    monkeypatch.setattr(sys, "argv", ["package-sparkle", str(stage), str(output), str(feed),
                                     VERSION, architecture, "true", prefix])
    exec(compile(python, "package-sparkle.sh:python", "exec"), {})
    target = f"SmartSearch-{VERSION}-from-1.0.0-macos-{architecture}.delta"
    assert (output / target).read_bytes() == delta.read_bytes()
    enclosures = list(ET.parse(output / feed.name).find("./channel/item").iter("enclosure"))
    assert len(enclosures) == 2
    assert enclosures[1].get("url") == prefix + target
    assert enclosures[1].get(f"{{{updates.SPARKLE}}}edSignature") == "fixture-delta-signature"
    assert len(list(output.iterdir())) == 3


@pytest.mark.parametrize("damage", [None, "unsigned", "corrupt", "architecture", "traversal", "duplicate", "missing-full", "missing-signature", "foreign-url"])
def test_release_assets_fail_closed(tmp_path, damage):
    release_files(tmp_path)
    windows = tmp_path / "releases.win-x64-stable.json"
    feed = json.loads(windows.read_text())
    asset = feed["Assets"][0]
    if damage == "unsigned":
        signed = tmp_path / f"SmartSearch-{VERSION}-win-x64-Setup-signed.exe"
        signed.rename(signed.with_name(signed.name.replace("-signed", "-unsigned-test")))
    elif damage == "corrupt":
        (tmp_path / asset["FileName"]).write_bytes(b"bad")
    elif damage == "architecture":
        asset["PackageId"] = "com.smartsearch.desktop.win-arm64"
    elif damage == "traversal":
        asset["FileName"] = "../escape.nupkg"
    elif damage == "duplicate":
        feed["Assets"].append(asset.copy())
    windows.write_text(json.dumps(feed))
    if damage in {"missing-full", "missing-signature", "foreign-url"}:
        path = tmp_path / "appcast-macos-arm64.xml"
        tree = ET.parse(path)
        item = tree.find("./channel/item")
        enclosure = item.find("enclosure")
        if damage == "missing-full":
            item.remove(enclosure)
        elif damage == "missing-signature":
            del enclosure.attrib[f"{{{updates.SPARKLE}}}edSignature"]
        else:
            enclosure.set("url", enclosure.get("url").replace("github.com", "invalid.example"))
        tree.write(path)
    if damage:
        with pytest.raises(ValueError):
            updates.validate_release(tmp_path, VERSION)
        assert not (tmp_path / "SHA256SUMS.txt").exists()
    else:
        assert updates.validate_release(tmp_path, VERSION)["assets"] == 12
        for row in (tmp_path / "SHA256SUMS.txt").read_text().splitlines():
            digest, name = row.split("  ")
            assert digest == hashlib.sha256((tmp_path / name).read_bytes()).hexdigest()


@pytest.mark.parametrize("failure", [None, "network", "corrupt", "no-baseline", "invalid-platform"])
def test_previous_release_integrity_and_first_release_are_distinct(tmp_path, monkeypatch, failure):
    package_name = "com.smartsearch.desktop.win-x64-1.0.0-full.nupkg"
    feed_name = "releases.win-x64-stable.json"
    package = b"verified previous package"
    feed = json.dumps({"Assets": [{"Type": "Full", "Version": "1.0.0",
        "PackageId": "com.smartsearch.desktop.win-x64", "FileName": package_name}]}).encode()
    data = {feed_name: feed, package_name: package}
    release = {"tag_name": "v1.0.0", "assets": [{"name": name, "size": len(content),
        "digest": "sha256:" + hashlib.sha256(content).hexdigest()} for name, content in data.items()]}

    def gh(*args):
        if failure == "network":
            raise subprocess.CalledProcessError(1, "gh")
        if args[0] == "api":
            return json.dumps([[] if failure == "no-baseline" else [release]])
        name = args[args.index("--pattern") + 1]
        output = Path(args[args.index("--dir") + 1])
        (output / name).write_bytes(b"tampered" if failure == "corrupt" else data[name])
        return ""

    monkeypatch.setattr(updates, "gh", gh)
    if failure in {"network", "corrupt", "invalid-platform"}:
        with pytest.raises((ValueError, subprocess.CalledProcessError)):
            updates.fetch_baseline(None if failure == "invalid-platform" else "windows", "x64", VERSION, tmp_path / "baseline")
    else:
        result = updates.fetch_baseline("windows", "x64", VERSION, tmp_path / "baseline")
        assert result["status"] == ("first-framework-release" if failure == "no-baseline" else "verified")
        if failure is None:
            assert (Path(result["directory"]) / package_name).read_bytes() == package
