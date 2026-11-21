"""Converts a DXF or DWG drawing to a DXF or DWG file, in a version of AutoCAD.

Run: python convert.py IN OUT [--version AC1027]   (IN and OUT end in .dxf or .dwg)
OUT is written in the version given, or in the version of IN. The DXF writer returns what it could not keep in that
version, and each is printed first, a line for each; then the file written and its version.
"""

import sys
from pathlib import Path

import geometryhelper  # noqa: F401  (starts .NET and loads the assemblies)
from GeometryHelper.Dwg import DwgReader, DwgWriter
from GeometryHelper.Dxf import DxfDatabase

USAGE = "usage: python convert.py IN OUT [--version ACnnnn]   (IN and OUT end in .dxf or .dwg)"


def open_drawing(path: Path) -> DxfDatabase:
    """Reads a DXF or a DWG file, by its extension."""
    return DwgReader.Open(str(path)) if path.suffix.lower() == ".dwg" else DxfDatabase.Open(str(path))


def main(argv: list[str]) -> int:
    version: str | None = None
    if len(argv) == 4 and argv[2] == "--version":
        version = argv[3]
        argv = argv[:2]
    if len(argv) != 2 or any(Path(name).suffix.lower() not in (".dxf", ".dwg") for name in argv):
        print(USAGE, file=sys.stderr)
        return 2
    source, target = Path(argv[0]), Path(argv[1])
    database = open_drawing(source)
    version = version or database.Version
    warnings: list[str] = []
    try:
        if target.suffix.lower() == ".dwg":
            DwgWriter.Write(database, str(target), version)
        else:
            warnings.extend(database.Save(str(target), version))
    except Exception as error:  # a version the writer does not write is refused; the first line of the error says which
        print(f"error: {str(error).splitlines()[0]}", file=sys.stderr)
        return 1
    for warning in warnings:
        print(f"warning: {warning}")
    print(f"wrote {target.name} ({version})")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
