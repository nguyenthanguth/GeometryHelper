"""A3: a host that already started a runtime (D604 step 1, D622). Cad.App will do this in P3: the package must start no
second runtime and load no second copy of an assembly the host has loaded.

Each case is a subprocess that plays the host with plain pythonnet calls, then imports geometryhelper.
"""

import shutil
from collections.abc import Callable
from pathlib import Path
from subprocess import CompletedProcess

import pytest

Python = Callable[..., CompletedProcess[str]]

ASSEMBLIES = ("GeometryHelper", "GeometryHelper.Cad.Remote", "GeometryHelper.Cad")

# argv: runtime, runtime config, folder to load the assemblies from ("-" loads none). Prints one line per assembly:
# its name, how many copies the AppDomain holds, and the location of the first.
HOST = """
import sys
import pythonnet
runtime, config, folder = sys.argv[1:4]
if runtime == "coreclr":
    pythonnet.load("coreclr", runtime_config=config)
else:
    pythonnet.load("netfx")
import clr
names = ("GeometryHelper", "GeometryHelper.Cad.Remote", "GeometryHelper.Cad")
if folder != "-":
    for name in names:
        clr.AddReference(folder + "/" + name + ".dll")
import geometryhelper
from System import AppDomain
for name in names:
    found = [a for a in AppDomain.CurrentDomain.GetAssemblies() if a.GetName().Name == name]
    print(name, len(found), found[0].Location)
"""


def _parse(output: str) -> dict[str, tuple[int, Path]]:
    rows: dict[str, tuple[int, Path]] = {}
    for line in output.splitlines():
        name, count, location = line.split(" ", 2)
        rows[name] = (int(count), Path(location).resolve())
    return rows


@pytest.mark.parametrize("runtime", ["coreclr", "netfx"])
def test_a_host_that_loaded_the_wheels_assemblies_gets_no_second_copy(
    python: Python, pkg_dir: Path, runtime: str
) -> None:
    config = str(pkg_dir / "geometryhelper.runtimeconfig.json")
    process = python(HOST, runtime, config, (pkg_dir / "lib").as_posix())
    assert process.returncode == 0, process.stderr
    assert _parse(process.stdout) == {name: (1, pkg_dir / "lib" / f"{name}.dll") for name in ASSEMBLIES}


@pytest.mark.parametrize("runtime", ["coreclr", "netfx"])
def test_a_host_that_loaded_its_own_copies_keeps_them_and_gets_no_second_copy(
    python: Python, pkg_dir: Path, tmp_path: Path, runtime: str
) -> None:
    # The host's copies are the same three files in another folder; after the import the AppDomain still holds one of
    # each and it is the host's, not the one beside the package.
    host_folder = tmp_path / "host"
    host_folder.mkdir()
    for name in ASSEMBLIES:
        shutil.copy2(pkg_dir / "lib" / f"{name}.dll", host_folder / f"{name}.dll")
    config = str(pkg_dir / "geometryhelper.runtimeconfig.json")
    process = python(HOST, runtime, config, host_folder.as_posix())
    assert process.returncode == 0, process.stderr
    assert _parse(process.stdout) == {name: (1, host_folder.resolve() / f"{name}.dll") for name in ASSEMBLIES}


@pytest.mark.parametrize("runtime", ["coreclr", "netfx"])
def test_a_host_that_only_started_the_runtime_gets_the_assemblies_added(
    python: Python, pkg_dir: Path, runtime: str
) -> None:
    config = str(pkg_dir / "geometryhelper.runtimeconfig.json")
    process = python(HOST, runtime, config, "-")
    assert process.returncode == 0, process.stderr
    assert _parse(process.stdout) == {name: (1, pkg_dir / "lib" / f"{name}.dll") for name in ASSEMBLIES}


@pytest.mark.parametrize("runtime", ["coreclr", "netfx"])
def test_runtime_names_the_runtime_a_host_started(python: Python, pkg_dir: Path, runtime: str) -> None:
    config = str(pkg_dir / "geometryhelper.runtimeconfig.json")
    process = python(HOST + "print(geometryhelper.runtime())\n", runtime, config, "-")
    assert process.returncode == 0, process.stderr
    assert process.stdout.splitlines()[-1] == runtime


def test_the_runtime_variable_is_not_read_when_a_host_started_a_runtime(python: Python, pkg_dir: Path) -> None:
    # Step 1 of D604: with clr in sys.modules nothing is started and the variable is not looked at, so a value that
    # would refuse the import on its own does not.
    config = str(pkg_dir / "geometryhelper.runtimeconfig.json")
    process = python(HOST, "coreclr", config, "-", env={"GEOMETRYHELPER_RUNTIME": "bogus"})
    assert process.returncode == 0, process.stderr
