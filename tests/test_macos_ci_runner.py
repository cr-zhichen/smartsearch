"""Run the CI entry point on the system Bash, including macOS Bash 3.2."""
import json
from pathlib import Path
import os
import subprocess
import sys

import pytest

SCRIPT = Path(__file__).resolve().parents[1] / "desktop/scripts/ci-macos.sh"
pytestmark = pytest.mark.skipif(not Path("/bin/bash").exists(), reason="Requires a POSIX shell")


@pytest.mark.parametrize("existing_build", [False, True])
def test_ci_checks_only_the_new_build_including_a_fresh_workspace(tmp_path, existing_build):
    if existing_build:
        old = tmp_path / ".desktop-artifacts/macos-old/result.json"
        old.parent.mkdir(parents=True)
        old.write_text("{}")
    commands = tmp_path / "bin"
    commands.mkdir()
    mise = commands / "mise"
    mise.write_text(f"#!{sys.executable}\n" + '''import json, sys
from pathlib import Path
if sys.argv[1:3] == ["run", "desktop:macos:build"]:
    result = Path(".desktop-artifacts/macos-new/result.json")
    result.parent.mkdir(parents=True)
    result.write_text("{}")
else:
    Path("checked.json").write_text(json.dumps(sys.argv[1:]))
''')
    mise.chmod(0o755)
    subprocess.run(["/bin/bash", SCRIPT, "--architecture", "arm64"], cwd=tmp_path,
                   env={**os.environ, "PATH": str(commands) + os.pathsep + os.environ["PATH"]}, check=True)
    assert json.loads((tmp_path / "checked.json").read_text()) == [
        "run", "python", "desktop/scripts/test_sparkle_updates.py", ".desktop-artifacts/macos-new/result.json",
    ]
