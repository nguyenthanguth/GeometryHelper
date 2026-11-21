"""A4: the support folders of the presenter are found beside GeometryHelper.Cad.dll, so ``lib/Fonts``,
``lib/Hatches`` and ``lib/Linetypes`` of the wheel need no code and no setting (research section 5)."""

from pathlib import Path

import pytest

import geometryhelper  # noqa: F401  (boots .NET and loads the assemblies)
from GeometryHelper.Cad import CadSupportFolders, CadSupportKind


def test_the_program_folder_is_the_lib_folder_of_the_wheel(lib_dir: Path) -> None:
    assert Path(str(CadSupportFolders.Default.ProgramFolder)).resolve() == lib_dir


@pytest.mark.parametrize("kind", ["Fonts", "Hatches", "Linetypes"])
def test_each_support_kind_is_found_inside_the_wheel(lib_dir: Path, kind: str) -> None:
    # The wheel ships one folder per kind, each with our own README and, for hatches and linetypes, our own file.
    existing = [Path(str(p)).resolve() for p in CadSupportFolders.Default.ExistingOf(getattr(CadSupportKind, kind))]
    assert existing == [lib_dir / kind]
    assert any(path.is_file() for path in (lib_dir / kind).iterdir())


def test_the_hatch_and_linetype_folders_hold_the_patterns_the_library_ships(lib_dir: Path) -> None:
    assert (lib_dir / "Hatches" / "GeometryHelper.pat").is_file()
    assert (lib_dir / "Linetypes" / "GeometryHelper.lin").is_file()
