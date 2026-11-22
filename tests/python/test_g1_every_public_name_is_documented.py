"""G1: every public name of ``geometryhelper.cad`` is documented (D1671): a docstring, an ``Example:`` block, and an
annotation on every parameter and on the return; and pyright strict finds nothing in the package.

A public name is a name of the module that is not marked private, defined in the package (a function or a class), and
every public method and property of such a class; ``Layers`` is counted too, which ``drawing.layers`` returns and the
module does not name. The package is typed inline and carries ``py.typed``, so for pure
Python modules that is the stub: no second copy is written.
"""

import inspect
import json
import subprocess
import sys
from pathlib import Path
from typing import Any

import pytest

import geometryhelper.cad as cad
from geometryhelper.cad._drawing import Layers

STRICT = Path(__file__).resolve().parent / "pyrightconfig.strict.json"
PREFIX = "geometryhelper.cad"


def _is_ours(thing: Any) -> bool:
    return getattr(thing, "__module__", "").startswith(PREFIX)


def _members(owner: type) -> list[tuple[str, Any]]:
    """The public methods and properties a class defines itself, by name (the base classes' are theirs to document)."""
    found: list[tuple[str, Any]] = []
    for name, value in vars(owner).items():
        if name.startswith("_"):
            continue
        if isinstance(value, property):
            found.append((name, value))
        elif isinstance(value, (staticmethod, classmethod)):
            found.append((name, value.__func__))
        elif inspect.isfunction(value):
            found.append((name, value))
    return found


def _public() -> list[tuple[str, Any]]:
    """(dotted name, object) of every public function, class, method and property of ``geometryhelper.cad``."""
    names: list[tuple[str, Any]] = []
    for name, value in sorted(vars(cad).items()):
        if name.startswith("_") or not (inspect.isfunction(value) or inspect.isclass(value)) or not _is_ours(value):
            continue
        names.append((name, value))
        if inspect.isclass(value):
            names.extend((f"{name}.{member}", thing) for member, thing in _members(value))
    names.append(("Layers", Layers))
    names.extend((f"Layers.{member}", thing) for member, thing in _members(Layers))
    return names


PUBLIC = _public()


def _doc(thing: Any) -> str:
    target = thing.fget if isinstance(thing, property) else thing
    return inspect.getdoc(target) or ""


def _function(thing: Any) -> Any:
    return thing.fget if isinstance(thing, property) else thing


def test_the_package_has_public_names_to_check() -> None:
    names = [name for name, _ in PUBLIC]

    assert len(names) > 100
    assert {"command", "cmd", "active_drawing", "connect", "open_file", "Drawing", "Drawing.add_line"} <= set(names)


@pytest.mark.parametrize(("name", "thing"), PUBLIC, ids=[name for name, _ in PUBLIC])
def test_a_public_name_has_a_docstring_with_an_example(name: str, thing: Any) -> None:
    doc = _doc(thing)

    assert doc.strip(), f"{PREFIX}.{name} has no docstring"
    assert "Example:" in doc, f"the docstring of {PREFIX}.{name} has no 'Example:' block"


@pytest.mark.parametrize(
    ("name", "thing"), [(n, t) for n, t in PUBLIC if not inspect.isclass(t)], ids=[n for n, t in PUBLIC if not inspect.isclass(t)]
)
def test_a_public_function_annotates_every_parameter_and_its_return(name: str, thing: Any) -> None:
    function = _function(thing)
    signature = inspect.signature(function)
    bare = [
        parameter.name
        for parameter in signature.parameters.values()
        if parameter.name not in ("self", "cls") and parameter.annotation is inspect.Parameter.empty
    ]

    assert bare == [], f"{PREFIX}.{name}: parameters with no annotation: {bare}"
    assert signature.return_annotation is not inspect.Signature.empty, f"{PREFIX}.{name} has no return annotation"


def test_a_constant_of_the_module_is_documented_in_the_source() -> None:
    source = Path(cad.__file__).read_text(encoding="utf-8")

    assert 'PAUSE: str = "\\\\"\n"""' in source


def test_pyright_strict_finds_nothing_in_the_package() -> None:
    folder = Path(cad.__file__).resolve().parent
    process = subprocess.run(
        [sys.executable, "-m", "pyright", "--outputjson", "-p", str(STRICT), "--pythonpath", sys.executable, str(folder)],
        capture_output=True,
        text=True,
        timeout=600,
    )
    assert process.stdout.startswith("{"), f"pyright gave no JSON (exit {process.returncode}): {process.stderr}"
    report = json.loads(process.stdout)
    assert report["summary"]["filesAnalyzed"] >= 5

    errors = [
        f"{Path(d['file']).name}:{d['range']['start']['line'] + 1}: {d.get('rule', '(no rule)')}: {d['message']}"
        for d in report["generalDiagnostics"]
        if d["severity"] == "error"
    ]

    assert errors == []
