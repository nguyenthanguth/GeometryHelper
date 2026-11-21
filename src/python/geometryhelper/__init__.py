"""GeometryHelper for Python: the .NET library and the CAD presenter, on pythonnet.

Importing this package starts a .NET runtime (unless one is already running) and loads GeometryHelper,
GeometryHelper.Cad.Remote and GeometryHelper.Cad from the ``lib`` folder beside this file. After that the .NET names
import as they are written in C#::

    from GeometryHelper.Geometry import GeoPoint3
    from GeometryHelper.Cad import CadDocument

The runtime is chosen by the environment variable ``GEOMETRYHELPER_RUNTIME``: ``auto`` (also when unset), ``coreclr``
(.NET 8 or later) or ``netfx`` (.NET Framework 4.8).
"""

from __future__ import annotations

import contextlib
import importlib
import importlib.metadata
import os
import sys
from collections.abc import Generator
from pathlib import Path
from typing import TYPE_CHECKING, Literal

if TYPE_CHECKING:
    from GeometryHelper.Cad import CadDocument

_DISTRIBUTION = "geometryhelper"

__version__: str = importlib.metadata.version(_DISTRIBUTION)

_FOLDER = Path(__file__).resolve().parent
_ASSEMBLIES = ("GeometryHelper", "GeometryHelper.Cad.Remote", "GeometryHelper.Cad")


def _has_dotnet8() -> bool:
    """Say whether a Microsoft.NETCore.App of major version 8 or more is installed."""
    import clr_loader

    return any(
        info.name == "Microsoft.NETCore.App" and int(info.version.split(".")[0]) >= 8
        for info in clr_loader.find_runtimes()
    )


def _start_runtime() -> None:
    """Starts coreclr or netfx through pythonnet; does nothing when pythonnet's ``clr`` is already imported."""
    if "clr" in sys.modules:
        return
    given = os.environ.get("GEOMETRYHELPER_RUNTIME", "")
    chosen = given.strip().lower()
    if chosen not in ("", "auto", "coreclr", "netfx"):
        raise ImportError(f"GEOMETRYHELPER_RUNTIME is '{given}': use auto, coreclr or netfx.")
    if chosen in ("", "auto"):
        chosen = "coreclr" if _has_dotnet8() else "netfx"
    elif chosen == "coreclr" and not _has_dotnet8():
        raise ImportError(
            "GEOMETRYHELPER_RUNTIME is coreclr but no .NET 8 or later is installed: install the .NET 8 runtime from "
            "https://dotnet.microsoft.com/download or set GEOMETRYHELPER_RUNTIME=netfx."
        )
    pythonnet = importlib.import_module("pythonnet")

    if chosen == "netfx":
        pythonnet.load("netfx")
    else:
        pythonnet.load("coreclr", runtime_config=str(_FOLDER / "geometryhelper.runtimeconfig.json"))


def _load_assemblies() -> None:
    """Adds each of our assemblies from ``lib``, unless one of that name is already loaded in the AppDomain."""
    clr = importlib.import_module("clr")
    app_domain = importlib.import_module("System").AppDomain
    loaded = {assembly.GetName().Name for assembly in app_domain.CurrentDomain.GetAssemblies()}
    for name in _ASSEMBLIES:
        if name not in loaded:
            clr.AddReference(str(_FOLDER / "lib" / (name + ".dll")))


_start_runtime()
_load_assemblies()


def runtime() -> Literal["coreclr", "netfx"]:
    """Say which .NET runtime the package runs on: ``netfx`` for .NET Framework, else ``coreclr``.

    It is true also when the runtime was started by someone else, such as the host application that embeds Python.
    """
    description = importlib.import_module("System.Runtime.InteropServices").RuntimeInformation.FrameworkDescription
    return "netfx" if str(description).startswith(".NET Framework") else "coreclr"


@contextlib.contextmanager
def undo_group(document: CadDocument, name: str) -> Generator[CadDocument, None, None]:
    """Make the edits of a ``with`` block one undo step of the document, or none.

    ``BeginGroup(name)`` runs on entry. ``EndGroup()`` runs when the block ends normally; ``CancelGroup()`` runs when
    it raises, and the exception goes on. A group left open by a forgotten ``EndGroup`` makes ``SaveAs`` throw.
    """
    document.BeginGroup(name)
    try:
        yield document
    except BaseException:
        document.CancelGroup()
        raise
    else:
        document.EndGroup()
