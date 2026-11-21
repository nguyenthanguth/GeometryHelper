"""A1: the loader. What ``import geometryhelper`` leaves in the process: version, runtime, the three assemblies."""

import importlib
import importlib.metadata as metadata
import json
from collections.abc import Callable
from pathlib import Path
from subprocess import CompletedProcess

import clr
import clr_loader
import pytest
from System import AppDomain
from System.Runtime.InteropServices import RuntimeInformation

import geometryhelper

ASSEMBLIES = ("GeometryHelper", "GeometryHelper.Cad.Remote", "GeometryHelper.Cad")

Python = Callable[..., CompletedProcess[str]]


def _loaded(simple_name: str) -> list[object]:
    return [a for a in AppDomain.CurrentDomain.GetAssemblies() if a.GetName().Name == simple_name]


def _installed_net_runtimes() -> list[tuple[int, ...]]:
    """The versions of every installed Microsoft.NETCore.App of major 8 or more, as int tuples, sorted."""
    versions = [
        tuple(int(part) for part in spec.version.split("."))
        for spec in clr_loader.find_runtimes()
        if spec.name == "Microsoft.NETCore.App"
    ]
    return sorted(v for v in versions if v[0] >= 8)


def test_version_is_the_assembly_version_without_its_fourth_part() -> None:
    # GeometryHelper.dll of 13.12.0 carries the AssemblyName version 13.12.0.0; __version__ drops the fourth part.
    from GeometryHelper.Geometry import GeoPoint3

    assembly_version = str(clr.GetClrType(GeoPoint3).Assembly.GetName().Version)
    assert geometryhelper.__version__ == ".".join(assembly_version.split(".")[:3])


def test_version_is_the_version_pip_installed() -> None:
    # One number for NuGet and pip: the metadata of the installed distribution says what __version__ says.
    distributions = metadata.packages_distributions()["geometryhelper"]
    assert [metadata.version(name) for name in distributions] == [geometryhelper.__version__]


def test_runtime_is_the_one_the_session_asked_for(session_runtime: str) -> None:
    description = str(RuntimeInformation.FrameworkDescription)
    assert description.startswith(".NET Framework") == (session_runtime == "netfx")
    assert geometryhelper.runtime() == session_runtime


@pytest.mark.parametrize("simple_name", ASSEMBLIES)
def test_each_assembly_is_loaded_once_and_from_the_wheel(simple_name: str, lib_dir: Path) -> None:
    loaded = _loaded(simple_name)
    assert len(loaded) == 1
    location = Path(str(loaded[0].Location)).resolve()
    assert location == lib_dir / f"{simple_name}.dll"


def test_importing_again_loads_no_assembly_a_second_time() -> None:
    # importlib.reload runs the boot code a second time: clr is in sys.modules and the assemblies are in the AppDomain.
    before = {name: len(_loaded(name)) for name in ASSEMBLIES}
    importlib.reload(geometryhelper)
    assert {name: len(_loaded(name)) for name in ASSEMBLIES} == before == dict.fromkeys(ASSEMBLIES, 1)


def test_the_library_folder_holds_the_three_assemblies_and_no_debug_files(lib_dir: Path) -> None:
    # D606: three DLLs and the support folders; the XML docs (17 MB) and the PDBs are not shipped.
    files = [p.relative_to(lib_dir).as_posix() for p in lib_dir.rglob("*") if p.is_file()]
    for simple_name in ASSEMBLIES:
        assert f"{simple_name}.dll" in files
    assert [f for f in files if f.lower().endswith((".xml", ".pdb"))] == []
    assert [f for f in files if "/" not in f and not f.endswith(".dll")] == []


def test_the_runtime_config_asks_for_net8_and_rolls_forward_by_major(pkg_dir: Path) -> None:
    config = json.loads((pkg_dir / "geometryhelper.runtimeconfig.json").read_text(encoding="utf-8"))
    assert config == {
        "runtimeOptions": {
            "tfm": "net8.0",
            "rollForward": "Major",
            "framework": {"name": "Microsoft.NETCore.App", "version": "8.0.0"},
        }
    }


def test_the_runtime_is_the_lowest_installed_major_from_8_with_its_latest_patch(python: Python) -> None:
    # T1: rollForward Major from 8.0.0. Where it was measured, 8.0.11 and 8.0.28 are installed beside 9 and 10,
    # and the process ran 8.0.28. The expectation is computed from clr_loader's list of what is installed.
    installed = _installed_net_runtimes()
    assert installed, "no .NET 8 or later is installed: install the .NET 8 runtime to run this suite"
    lowest_major = installed[0][0]
    expected = ".".join(str(part) for part in max(v for v in installed if v[0] == lowest_major))
    process = python(
        "import geometryhelper\n"
        "from System.Runtime.InteropServices import RuntimeInformation\n"
        "print(RuntimeInformation.FrameworkDescription)\n",
        env={"GEOMETRYHELPER_RUNTIME": "coreclr", "DOTNET_ROLL_FORWARD": None},
    )
    assert process.returncode == 0, process.stderr
    assert process.stdout.strip() == f".NET {expected}"


def test_dotnet_roll_forward_in_the_environment_wins_over_the_runtime_config(python: Python) -> None:
    # T1: DOTNET_ROLL_FORWARD=LatestMajor overrides the "Major" of the file: the newest installed runtime is started
    # (10.0.13 where it was measured).
    installed = _installed_net_runtimes()
    assert installed, "no .NET 8 or later is installed: install the .NET 8 runtime to run this suite"
    expected = ".".join(str(part) for part in installed[-1])
    process = python(
        "import geometryhelper\n"
        "from System.Runtime.InteropServices import RuntimeInformation\n"
        "print(RuntimeInformation.FrameworkDescription)\n",
        env={"GEOMETRYHELPER_RUNTIME": "coreclr", "DOTNET_ROLL_FORWARD": "LatestMajor"},
    )
    assert process.returncode == 0, process.stderr
    assert process.stdout.strip() == f".NET {expected}"
