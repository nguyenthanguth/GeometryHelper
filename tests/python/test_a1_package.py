"""A1 (package): what the installed wheel says about itself: metadata, tag, licence, and the files it holds (D602, D603,
D606, D609)."""

import importlib.metadata as metadata
from importlib.metadata import Distribution
from pathlib import Path, PurePosixPath

import pytest
from packaging.requirements import Requirement
from packaging.specifiers import SpecifierSet

REPOSITORY = Path(__file__).resolve().parents[2]


def _distribution() -> Distribution:
    # The distribution name is a constant of the package (D619) and may change; the import name does not.
    (name,) = metadata.packages_distributions()["geometryhelper"]
    return metadata.distribution(name)


def _files() -> list[str]:
    files = _distribution().files
    assert files is not None
    return sorted(
        PurePosixPath(*file.parts).as_posix() for file in files if "__pycache__" not in file.parts
    )


def test_the_wheel_is_tagged_for_any_python_and_64_bit_windows() -> None:
    wheel = _distribution().read_text("WHEEL")
    assert wheel is not None
    tags = [line.split(": ", 1)[1] for line in wheel.splitlines() if line.startswith("Tag: ")]
    assert tags == ["py3-none-win_amd64"]
    assert "Root-Is-Purelib: false" in wheel.splitlines()


def test_the_metadata_asks_for_python_311_to_315_and_pythonnet_and_offers_numpy() -> None:
    meta = _distribution().metadata
    assert SpecifierSet(meta["Requires-Python"]) == SpecifierSet(">=3.11,<3.16")
    requirements = [Requirement(text) for text in _distribution().requires or []]
    required = [(r.name, r.specifier) for r in requirements if r.marker is None]
    assert required == [("pythonnet", SpecifierSet(">=3.2.1,<4"))]
    optional = [(r.name, r.specifier, str(r.marker)) for r in requirements if r.marker is not None]
    assert optional == [("numpy", SpecifierSet(">=1.26"), 'extra == "numpy"')]
    assert meta.get_all("Provides-Extra", []) == ["numpy"]
    # PEP 639: the licence is an SPDX expression, and a licence classifier may not stand beside it.
    assert meta["License-Expression"] == "MIT"
    classifiers = meta.get_all("Classifier", [])
    assert not [c for c in classifiers if c.startswith("License ::")]
    for classifier in (
        "Operating System :: Microsoft :: Windows",
        *(f"Programming Language :: Python :: 3.{minor}" for minor in range(11, 16)),
    ):
        assert classifier in classifiers


def test_the_licence_is_in_the_dist_info() -> None:
    licences = [f for f in _files() if f.endswith("/licenses/LICENSE")]
    assert len(licences) == 1
    shipped = Path(str(_distribution().locate_file(licences[0]))).read_bytes()
    assert shipped == (REPOSITORY / "LICENSE").read_bytes()


def test_the_wheel_holds_the_files_of_the_list_and_nothing_else() -> None:
    files = _files()
    own = [f for f in files if f.startswith("geometryhelper/")]
    assert [f for f in own if not f.startswith("geometryhelper/lib/")] == [
        "geometryhelper/__init__.py",
        "geometryhelper/_app.py",
        "geometryhelper/arrays.py",
        "geometryhelper/cad.py",
        "geometryhelper/geometryhelper.runtimeconfig.json",
        "geometryhelper/py.typed",
    ]
    assert [f for f in files if not f.startswith(("geometryhelper/", "GeometryHelper-stubs/", "System-stubs/"))
            and ".dist-info/" not in f] == []


@pytest.mark.parametrize("folder", ["Fonts", "Hatches", "Linetypes"])
def test_the_lib_folder_has_each_support_folder_with_files_in_it(folder: str) -> None:
    assert [f for f in _files() if f.startswith(f"geometryhelper/lib/{folder}/")] != []


def test_the_stub_packages_are_there_with_one_module_per_namespace() -> None:
    files = _files()
    assert "GeometryHelper-stubs/__init__.pyi" in files
    assert "System-stubs/__init__.pyi" in files
    modules = sorted(
        PurePosixPath(f).parent.as_posix()
        for f in files
        if f.startswith("GeometryHelper-stubs/") and f.endswith("/__init__.pyi")
    )
    # 18 namespaces in the three assemblies, the root GeometryHelper among them (research section 3).
    assert len(modules) == 18
    assert [f for f in files if f.startswith("GeometryHelper-stubs/") and f.endswith(".py")] == []
    assert [f for f in files if f.startswith("System-stubs/") and f.endswith(".py")] == []
