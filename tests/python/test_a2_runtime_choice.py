"""A2: choosing the runtime (D604): the values of GEOMETRYHELPER_RUNTIME and the two error texts.

Every case runs in a subprocess, so the choice is made by a fresh ``import geometryhelper`` whatever runtime the
pytest process itself runs on.
"""

from collections.abc import Callable
from subprocess import CompletedProcess

import clr_loader
import pytest

Python = Callable[..., CompletedProcess[str]]

BOGUS_TEXT = "ImportError: GEOMETRYHELPER_RUNTIME is 'bogus': use auto, coreclr or netfx."
NO_NET8_TEXT = (
    "ImportError: GEOMETRYHELPER_RUNTIME is coreclr but no .NET 8 or later is installed: install the .NET 8 runtime "
    "from https://dotnet.microsoft.com/download or set GEOMETRYHELPER_RUNTIME=netfx."
)

PRINT_RUNTIME = "import geometryhelper\nprint(geometryhelper.runtime())\n"

# What `clr_loader.find_runtimes()` lists, replaced before geometryhelper is imported. The spec type is the one
# clr_loader returns, built here with the same three fields.
FAKE_RUNTIMES = (
    "import clr_loader\n"
    "Spec = type(next(iter(clr_loader.find_runtimes())))\n"
    "versions = {versions!r}\n"
    "clr_loader.find_runtimes = lambda: [Spec('Microsoft.NETCore.App', v, '/x') for v in versions]\n"
)


def _auto_expected() -> str:
    """What ``auto`` must give on this machine: coreclr when a Microsoft.NETCore.App of major 8 or more is installed."""
    return (
        "coreclr"
        if any(
            spec.name == "Microsoft.NETCore.App" and int(spec.version.split(".")[0]) >= 8
            for spec in clr_loader.find_runtimes()
        )
        else "netfx"
    )


def test_a_bogus_value_is_refused_with_the_documented_text(python: Python) -> None:
    process = python("import geometryhelper\n", env={"GEOMETRYHELPER_RUNTIME": "bogus"})
    assert process.returncode == 1
    assert BOGUS_TEXT in process.stderr.splitlines()


@pytest.mark.parametrize("value", [None, "", "auto", "AUTO"])
def test_auto_unset_or_empty_gives_coreclr_when_net8_is_installed(python: Python, value: str | None) -> None:
    # The machine under test has .NET 8 or later (the suite needs it for coreclr); the oracle is clr_loader.
    process = python(PRINT_RUNTIME, env={"GEOMETRYHELPER_RUNTIME": value})
    assert process.returncode == 0, process.stderr
    assert process.stdout.strip() == _auto_expected() == "coreclr"


@pytest.mark.parametrize(
    "value, expected", [("coreclr", "coreclr"), ("CoreCLR", "coreclr"), ("netfx", "netfx"), ("NetFx", "netfx")]
)
def test_the_values_are_read_with_the_case_ignored(python: Python, value: str, expected: str) -> None:
    process = python(PRINT_RUNTIME, env={"GEOMETRYHELPER_RUNTIME": value})
    assert process.returncode == 0, process.stderr
    assert process.stdout.strip() == expected


def test_auto_falls_back_to_netfx_when_only_net6_is_installed(python: Python) -> None:
    # find_runtimes patched to list only a .NET 6: major 6 is below 8, so auto is netfx.
    process = python(
        FAKE_RUNTIMES.format(versions=["6.0.36"]) + PRINT_RUNTIME, env={"GEOMETRYHELPER_RUNTIME": "auto"}
    )
    assert process.returncode == 0, process.stderr
    assert process.stdout.strip() == "netfx"


def test_auto_takes_coreclr_when_the_only_net8_or_later_listed_is_9(python: Python) -> None:
    # The list is patched to a .NET 6 and a .NET 9: auto looks for a major of 8 or more, finds 9 and takes coreclr. What
    # really starts is the machine's own runtime (the patch changes what the package is told, not what is installed).
    process = python(
        FAKE_RUNTIMES.format(versions=["6.0.36", "9.0.1"]) + PRINT_RUNTIME, env={"GEOMETRYHELPER_RUNTIME": "auto"}
    )
    assert process.returncode == 0, process.stderr
    assert process.stdout.strip() == "coreclr"


def test_coreclr_without_a_net8_is_refused_with_the_documented_text(python: Python) -> None:
    process = python(
        FAKE_RUNTIMES.format(versions=["6.0.36", "7.0.20"]) + "import geometryhelper\n",
        env={"GEOMETRYHELPER_RUNTIME": "coreclr"},
    )
    assert process.returncode == 1
    assert NO_NET8_TEXT in process.stderr.splitlines()


def test_coreclr_with_no_runtime_listed_at_all_is_refused_with_the_documented_text(python: Python) -> None:
    process = python(
        FAKE_RUNTIMES.format(versions=[]) + "import geometryhelper\n", env={"GEOMETRYHELPER_RUNTIME": "coreclr"}
    )
    assert process.returncode == 1
    assert NO_NET8_TEXT in process.stderr.splitlines()


def test_the_pythonnet_variable_is_not_read(python: Python) -> None:
    # PYTHONNET_RUNTIME=netfx would start .NET Framework if pythonnet were left to choose; the package asks for coreclr.
    process = python(
        "import geometryhelper\n"
        "from System.Runtime.InteropServices import RuntimeInformation\n"
        "print(RuntimeInformation.FrameworkDescription)\n",
        env={"GEOMETRYHELPER_RUNTIME": "coreclr", "PYTHONNET_RUNTIME": "netfx"},
    )
    assert process.returncode == 0, process.stderr
    assert process.stdout.startswith(".NET ") and not process.stdout.startswith(".NET Framework")
