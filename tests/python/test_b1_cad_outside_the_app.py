"""B1: ``geometryhelper.cad`` outside GeometryHelper CAD (P3 D709, D702). The module reaches the host through a .NET class
that only the app loads, so a script run by plain Python gets one clear ImportError, and the rest of the package is not
touched by it. Each case is a subprocess, as the cases of A2 and A3 are."""

from collections.abc import Callable
from subprocess import CompletedProcess

Python = Callable[..., CompletedProcess[str]]

MESSAGE = "geometryhelper.cad works inside GeometryHelper CAD only: in the PYTHON palette, PYRUN or PYLOAD."


def test_importing_the_cad_module_outside_the_app_says_where_it_works(python: Python) -> None:
    process = python("import geometryhelper.cad")
    assert process.returncode != 0
    assert f"ImportError: {MESSAGE}" in process.stderr


def test_importing_the_module_by_from_gives_the_same_message(python: Python) -> None:
    process = python("from geometryhelper import cad")
    assert process.returncode != 0
    assert f"ImportError: {MESSAGE}" in process.stderr


def test_a_script_can_catch_the_import_error_and_go_on(python: Python) -> None:
    code = "\n".join(
        [
            "try:",
            "    import geometryhelper.cad",
            "except ImportError as error:",
            "    print(error)",
            "import geometryhelper",
            "print(geometryhelper.runtime() in ('coreclr', 'netfx'))",
        ]
    )
    process = python(code)
    assert process.returncode == 0, process.stderr
    assert process.stdout.splitlines() == [MESSAGE, "True"]


def test_importing_geometryhelper_alone_does_not_import_the_cad_module(python: Python) -> None:
    process = python("import sys, geometryhelper; print('geometryhelper.cad' in sys.modules)")
    assert process.returncode == 0, process.stderr
    assert process.stdout.strip() == "False"
