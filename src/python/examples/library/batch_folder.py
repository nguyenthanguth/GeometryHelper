"""Reads every DXF and DWG file of a folder and writes one line each to a CSV report.

Run: python batch_folder.py FOLDER REPORT.csv
The report has the columns file, version, objects, layers and error. A file that cannot be read is a row with its
error, and the next file goes on. Subfolders are not entered. The exit code is 1 when any file failed, else 0.
"""

import csv
import sys
from pathlib import Path

import geometryhelper  # noqa: F401  (starts .NET and loads the assemblies)
from GeometryHelper.Dwg import DwgReader
from GeometryHelper.Dxf import DxfDatabase


def open_drawing(path: Path) -> DxfDatabase:
    """Reads a DXF or a DWG file, by its extension."""
    return DwgReader.Open(str(path)) if path.suffix.lower() == ".dwg" else DxfDatabase.Open(str(path))


def main(argv: list[str]) -> int:
    if len(argv) != 2 or not Path(argv[0]).is_dir():
        print("usage: python batch_folder.py FOLDER REPORT.csv", file=sys.stderr)
        return 2
    files = [path for path in Path(argv[0]).iterdir() if path.is_file() and path.suffix.lower() in (".dxf", ".dwg")]
    files.sort(key=lambda path: (path.name.casefold(), path.name))
    failed = 0
    with open(argv[1], "w", newline="", encoding="utf-8") as report:
        writer = csv.writer(report, lineterminator="\n")
        writer.writerow(["file", "version", "objects", "layers", "error"])
        for path in files:
            try:
                database = open_drawing(path)
            except Exception as error:  # a .NET exception arrives as a Python one; its first line says what is wrong
                failed += 1
                writer.writerow([path.name, "", "", "", str(error).splitlines()[0]])
            else:
                layers = sum(1 for _ in database.LayerTable)
                writer.writerow([path.name, database.Version, database.ModelSpace.Count, layers, ""])
    print(f"{len(files)} files, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
