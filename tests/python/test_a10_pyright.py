"""A10: pyright on what a user writes (D615): the acceptance script and the ``ok_*`` files type-check clean; each
``bad_*`` file gives exactly the errors marked on its lines; the installed package is clean in strict mode; the
installed stubs are clean.

One pyright run covers the scripts of the first three checks (a run costs seconds). A rule name in `# expect:` is the
pyright rule of the error; `a|b` lists rules that are the same mistake to a user and differ with how the overloads
are written.
"""

import functools
import importlib.metadata as metadata
import json
import re
import subprocess
import sys
from pathlib import Path
from typing import Any

import pytest

HERE = Path(__file__).resolve().parent
CONFIG = HERE / "pyrightconfig.json"
STRICT = HERE / "pyrightconfig.strict.json"
STUBS = HERE / "pyrightconfig.stubs.json"
SCRIPTS = [HERE / "acceptance" / "acceptance.py", *sorted((HERE / "typecheck").glob("*.py"))]
BAD = [path for path in SCRIPTS if path.name.startswith("bad_")]
CLEAN = [path for path in SCRIPTS if not path.name.startswith("bad_")]
EXPECT = re.compile(r"# expect: (\S+)")


def _pyright(config: Path, *paths: Path) -> list[dict[str, Any]]:
    """The error diagnostics (not warnings) pyright reports for ``paths``, as the dictionaries of its JSON output."""
    process = subprocess.run(
        [sys.executable, "-m", "pyright", "--outputjson", "-p", str(config), "--pythonpath", sys.executable]
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


@functools.cache
def _scripts_errors() -> tuple[tuple[str, int, str, str], ...]:
    """(file name, line from 1, rule, message) of every error in the acceptance script and the typecheck files."""
    return tuple(
        (Path(d["file"]).name, d["range"]["start"]["line"] + 1, d.get("rule", "(no rule)"), d["message"])
        for d in _pyright(CONFIG, *SCRIPTS)
    )


def _site_packages() -> Path:
    import geometryhelper

    return Path(geometryhelper.__file__).resolve().parent.parent


def test_the_pyright_that_runs_is_the_one_the_requirements_pin() -> None:
    pinned = dict(line.split("==") for line in (HERE / "requirements.txt").read_text(encoding="utf-8").split())
    assert metadata.version("pyright") == pinned["pyright"]


@pytest.mark.parametrize("script", CLEAN, ids=lambda p: p.name)
def test_a_script_written_right_has_no_error(script: Path) -> None:
    assert [e for e in _scripts_errors() if e[0] == script.name] == []


@pytest.mark.parametrize("script", BAD, ids=lambda p: p.name)
def test_a_script_written_wrong_has_exactly_the_marked_errors(script: Path) -> None:
    expected = {
        number: set(match.group(1).split("|"))
        for number, text in enumerate(script.read_text(encoding="utf-8").splitlines(), start=1)
        if (match := EXPECT.search(text))
    }
    assert expected, f"{script.name} marks no error"
    found: dict[int, list[tuple[str, str]]] = {}
    for name, number, rule, message in _scripts_errors():
        if name == script.name:
            found.setdefault(number, []).append((rule, message))
    assert sorted(found) == sorted(expected), f"errors on lines {sorted(found)}, marked {sorted(expected)}: {found}"
    for number, errors in found.items():
        assert {rule for rule, _ in errors} <= expected[number], f"line {number}: {errors}"


def test_the_installed_package_is_clean_in_strict_mode() -> None:
    package = _site_packages() / "geometryhelper"
    assert _pyright(STRICT, *sorted(package.glob("*.py"))) == []


def test_the_installed_stubs_are_clean() -> None:
    # pyrightconfig.stubs.json turns off three rules about how a stub relates to its own base classes and overloads
    # (an override with a narrower return type, as .NET allows; the tuple of an out-parameter overload that looks like
    # another overload). A user is never shown them (pyright reports nothing inside a library's stubs); a
    # method order that cannot be resolved, a bad overload mix or a syntax error still count.
    site = _site_packages()
    assert _pyright(STUBS, site / "GeometryHelper-stubs", site / "System-stubs") == []
