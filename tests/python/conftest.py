"""Fixtures and the session guard of the Python suite.

The suite runs only against the built wheel installed in a fresh virtual environment, on the .NET runtime named by
GEOMETRYHELPER_RUNTIME. The guard stops the session when either is not so: a green run against the source tree, or on
the other runtime than the one the report names, would say nothing about the wheel.
"""

import os
import subprocess
import sys
import sysconfig
from collections.abc import Callable
from pathlib import Path
from typing import Any

import pytest


def _installed_under_site_packages(package_file: str) -> bool:
    folder = Path(package_file).resolve()
    roots = {Path(sysconfig.get_paths()[key]).resolve() for key in ("purelib", "platlib")}
    return any(root in folder.parents for root in roots)


def pytest_sessionstart(session: pytest.Session) -> None:
    asked = os.environ.get("GEOMETRYHELPER_RUNTIME", "")
    if asked not in ("coreclr", "netfx"):
        pytest.exit(
            f"GEOMETRYHELPER_RUNTIME is '{asked}': set it to coreclr or netfx, so the runtime under test is named.",
            returncode=3,
        )
    import geometryhelper

    if not _installed_under_site_packages(geometryhelper.__file__):
        pytest.exit(
            f"geometryhelper was imported from {geometryhelper.__file__}, not from the site-packages of the venv "
            "with the wheel installed: install the wheel into a fresh venv and run pytest there.",
            returncode=3,
        )
    # The runtime is read from .NET itself, not from geometryhelper.runtime(): the function is under test.
    from System.Runtime.InteropServices import RuntimeInformation

    running = "netfx" if RuntimeInformation.FrameworkDescription.startswith(".NET Framework") else "coreclr"
    if running != asked:
        pytest.exit(f"GEOMETRYHELPER_RUNTIME is {asked} but the process runs {running}.", returncode=3)


@pytest.fixture(scope="session")
def pkg_dir() -> Path:
    """The folder of the installed package ``geometryhelper``."""
    import geometryhelper

    return Path(geometryhelper.__file__).resolve().parent


@pytest.fixture(scope="session")
def lib_dir(pkg_dir: Path) -> Path:
    """The ``lib`` folder of the wheel: the three assemblies and the support folders."""
    return pkg_dir / "lib"


@pytest.fixture(scope="session")
def session_runtime() -> str:
    """``coreclr`` or ``netfx``: the runtime this pytest run was asked to test."""
    return os.environ["GEOMETRYHELPER_RUNTIME"]


@pytest.fixture
def python() -> Callable[..., subprocess.CompletedProcess[str]]:
    """Runs ``python -I -c <code> <args>`` of the venv under test and returns the finished process.

    ``env`` entries are added to a copy of the environment of the run; a value of ``None`` removes the entry. Every
    run has a timeout of 120 s, so a hung runtime fails the test instead of the session.
    """

    def run(code: str, *args: str, env: dict[str, str | None] | None = None) -> subprocess.CompletedProcess[str]:
        child_env = dict(os.environ)
        for name, value in (env or {}).items():
            if value is None:
                child_env.pop(name, None)
            else:
                child_env[name] = value
        return subprocess.run(
            [sys.executable, "-I", "-c", code, *args],
            capture_output=True,
            text=True,
            timeout=120,
            env=child_env,
        )

    return run


@pytest.fixture
def lines_database() -> Callable[[int], Any]:
    """Builds a drawing of ``count`` vertical lines, line ``i`` from (10 i, 0, 0) to (10 i, 50, 0), made in code.

    A new drawing has no public constructor: it is the text of an empty ``DxfWriter`` parsed (the guide's how-to).
    """

    def build(count: int) -> Any:
        from GeometryHelper.Dxf import DxfDatabase, DxfLine, DxfWriter
        from GeometryHelper.Geometry import GeoPoint3

        database = DxfDatabase.Parse(DxfWriter().ToString())
        for i in range(count):
            database.ModelSpace.AppendEntity(DxfLine(GeoPoint3(10 * i, 0, 0), GeoPoint3(10 * i, 50, 0)))
        return database

    return build
