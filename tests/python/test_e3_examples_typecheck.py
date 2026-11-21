"""E3: every example under ``src/python/examples`` type-checks clean in pyright (P5 D1007): one run, over the files of
the three folders, with the configuration the scripts of A10 are checked with. The examples are what a user copies, so
they are held to what the stubs promise."""

import json
import subprocess
import sys
from pathlib import Path
from typing import Any

HERE = Path(__file__).resolve().parent
EXAMPLES = HERE.parents[1] / "src" / "python" / "examples"
CONFIG = HERE / "pyrightconfig.json"


def _pyright(*paths: Path) -> list[dict[str, Any]]:
    """The error diagnostics (not warnings) pyright reports for ``paths``, as the dictionaries of its JSON output."""
    process = subprocess.run(
        [sys.executable, "-m", "pyright", "--outputjson", "-p", str(CONFIG), "--pythonpath", sys.executable]
        + [str(p) for p in paths],
        capture_output=True,
        text=True,
        timeout=600,
    )
    assert process.stdout.startswith("{"), f"pyright gave no JSON (exit {process.returncode}): {process.stderr}"
    report = json.loads(process.stdout)
    analysed = report["summary"]["filesAnalyzed"]
    assert analysed >= len(paths), f"pyright analysed {analysed} files for {len(paths)} paths given"
    return [d for d in report["generalDiagnostics"] if d["severity"] == "error"]


def test_every_example_has_no_pyright_error() -> None:
    files = sorted(EXAMPLES.rglob("*.py"))
    assert files, f"there is no example under {EXAMPLES}"

    errors = _pyright(*files)

    assert [
        f"{Path(d['file']).relative_to(EXAMPLES).as_posix()}:{d['range']['start']['line'] + 1}: "
        f"{d.get('rule', '(no rule)')}: {d['message']}"
        for d in errors
    ] == []
