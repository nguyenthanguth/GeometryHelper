"""A9: the acceptance script runs as a program and exits 0 (D624)."""

import subprocess
import sys
from pathlib import Path

ACCEPTANCE = Path(__file__).resolve().parent / "acceptance" / "acceptance.py"


def _run() -> subprocess.CompletedProcess[str]:
    return subprocess.run([sys.executable, "-I", str(ACCEPTANCE)], capture_output=True, text=True, timeout=120)


def test_the_acceptance_script_exits_zero_on_the_runtime_under_test(session_runtime: str) -> None:
    process = _run()
    assert process.returncode == 0, process.stderr
    lines = process.stdout.splitlines()
    assert lines[0].endswith(f"on {session_runtime}")
    assert lines[-1] == "acceptance: ok"


def test_the_acceptance_script_run_twice_gives_the_same_output() -> None:
    # The script owns its temporary folder and removes it: a second run meets nothing the first left.
    first, second = _run(), _run()
    assert (first.returncode, second.returncode) == (0, 0)
    assert first.stdout == second.stdout
