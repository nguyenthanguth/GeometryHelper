"""B1: ``geometryhelper.cad`` outside GeometryHelper CAD (P3 D709, D702, D1650). The module imports everywhere: a script run by
plain Python gets the module, and a function that needs the app raises ``RuntimeError`` naming itself when it is called. Each
case is a subprocess, as the cases of A2 and A3 are."""

from collections.abc import Callable
from subprocess import CompletedProcess

Python = Callable[..., CompletedProcess[str]]


def test_importing_the_cad_module_outside_the_app_works(python: Python) -> None:
    process = python("import geometryhelper.cad")
    assert process.returncode == 0, process.stderr


def test_importing_the_module_by_from_works_too(python: Python) -> None:
    process = python("from geometryhelper import cad; print(cad.PAUSE)")
    assert process.returncode == 0, process.stderr
    assert process.stdout.strip() == "\\"


def test_a_function_that_needs_the_app_says_so_when_it_is_called(python: Python) -> None:
    code = "\n".join(
        [
            "import geometryhelper.cad as cad",
            "try:",
            "    cad.get_point('Where')",
            "except RuntimeError as error:",
            "    print(error)",
        ]
    )
    process = python(code)
    assert process.returncode == 0, process.stderr
    assert process.stdout.splitlines() == ["get_point works inside GeometryHelper CAD only"]


def test_importing_geometryhelper_alone_does_not_import_the_cad_module(python: Python) -> None:
    process = python("import sys, geometryhelper; print('geometryhelper.cad' in sys.modules)")
    assert process.returncode == 0, process.stderr
    assert process.stdout.strip() == "False"
