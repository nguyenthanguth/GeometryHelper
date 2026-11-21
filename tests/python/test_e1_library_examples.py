"""E1: the examples of ``src/python/examples/library`` run as a user runs them (P5 D1002, D1007): ``python -I <file>
<arguments>`` in the venv the wheel is installed in, each on a drawing built here in code, never a file of the
repository.

The drawing S is the one of D1006: layers 0, Empty, Slabs and Walls; on 0 one line; on Slabs a closed polyline 4000 by
3000 (area 12 000 000) and a circle of radius 300 (area 282 743.339); on Walls an open polyline (0, 4000) (1000, 4000)
(1000, 5000) and two lines. It is written as DXF with ``DxfDatabase.Save`` and as DWG with ``DwgWriter.Write``.

The versions of the files (``AC1015`` and ``AC1032`` in the run these tests were drafted on) are read from the bytes of
the files, never from the library, and the first line of the error of a file that is no DXF is pinned from a run
(D1011).
"""

import csv
import hashlib
import re
import subprocess
import sys
from pathlib import Path
from typing import Any

import pytest

import geometryhelper  # noqa: F401  (boots .NET and loads the assemblies)
from GeometryHelper.Cad import CadDocument
from GeometryHelper.Dwg import DwgWriter
from GeometryHelper.Dxf import DxfCircle, DxfDatabase, DxfLayerTableRecord, DxfLine, DxfPolyline, DxfWriter
from GeometryHelper.Geometry import GeoPoint2, GeoPoint3

LIBRARY = Path(__file__).resolve().parents[2] / "src" / "python" / "examples" / "library"
# The start of the FormatException the library throws for a file of the text "not a drawing" (read from a run).
NOT_A_DXF = "This is not a DXF that can be read"
S_LAYERS = ["0", "Empty", "Slabs", "Walls"]


def _polyline(points: list[tuple[float, float]], closed: bool, layer: str) -> Any:
    made = DxfPolyline()
    for index, (x, y) in enumerate(points):
        made.AddVertexAt(index, GeoPoint2(x, y), 0.0, 0.0, 0.0)
    made.Closed = closed
    made.Layer = layer
    return made


def _line(start: tuple[float, float], end: tuple[float, float], layer: str) -> Any:
    made = DxfLine(GeoPoint3(start[0], start[1], 0), GeoPoint3(end[0], end[1], 0))
    made.Layer = layer
    return made


def _drawing_s() -> Any:
    database = DxfDatabase.Parse(DxfWriter().ToString())
    for name in ("Empty", "Slabs", "Walls"):
        layer = DxfLayerTableRecord()
        layer.Name = name
        database.LayerTable.Add(layer)
    circle = DxfCircle()
    circle.Center = GeoPoint3(2000, 1500, 0)
    circle.Radius = 300.0
    circle.Layer = "Slabs"
    for entity in (
        _line((0, 0), (100, 0), "0"),
        _polyline([(0, 0), (4000, 0), (4000, 3000), (0, 3000)], True, "Slabs"),
        circle,
        _polyline([(0, 4000), (1000, 4000), (1000, 5000)], False, "Walls"),
        _line((0, 6000), (500, 6000), "Walls"),
        _line((0, 6500), (500, 6500), "Walls"),
    ):
        database.ModelSpace.AppendEntity(entity)
    return database


def _write_s(path: Path) -> Path:
    """Writes the drawing S to ``path``: a DXF or a DWG by its extension."""
    database = _drawing_s()
    if path.suffix == ".dwg":
        DwgWriter.Write(database, str(path))
    else:
        database.Save(str(path))
    return path


@pytest.fixture
def plan_dxf(tmp_path: Path) -> Path:
    return _write_s(tmp_path / "plan.dxf")


@pytest.fixture
def plan_dwg(tmp_path: Path) -> Path:
    return _write_s(tmp_path / "plan.dwg")


@pytest.fixture
def work(tmp_path: Path) -> Path:
    """An empty folder every example runs in, so that a file it wrote by accident shows."""
    folder = tmp_path / "work"
    folder.mkdir()
    return folder


def _run(example: str, *args: object, cwd: Path) -> "subprocess.CompletedProcess[str]":
    """Runs ``python -I library/<example> <args>`` of the venv under test, in ``cwd``, with a timeout of 120 s."""
    script = LIBRARY / example
    assert script.is_file(), f"{script} does not exist: the example is not written"
    return subprocess.run(
        [sys.executable, "-I", str(script), *map(str, args)],
        capture_output=True,
        text=True,
        timeout=120,
        cwd=cwd,
    )


def _sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _version_of(path: Path) -> str:
    """The version a DXF or a DWG file says it is, read from its bytes: the six characters a DWG starts with, the
    ``$ACADVER`` of the header of a DXF."""
    data = path.read_bytes()
    if path.suffix == ".dwg":
        return data[:6].decode("ascii")
    found = re.search(rb"\$ACADVER\r?\n\s*1\r?\n(AC\d{4})\r?\n", data)
    assert found is not None, f"{path.name} has no $ACADVER"
    return found.group(1).decode("ascii")


def _objects(path: Path) -> list[Any]:
    return list(CadDocument.Open(str(path)).Database.ModelSpace)


def _layer_names(path: Path) -> list[str]:
    return [str(layer.Name) for layer in CadDocument.Open(str(path)).Database.LayerTable]


# ---- list_layers.py ----------------------------------------------------------------------------------------------


@pytest.mark.parametrize("extension", ["dxf", "dwg"])
def test_list_layers_prints_a_line_for_each_layer_sorted_with_a_total(
    request: pytest.FixtureRequest, work: Path, extension: str
) -> None:
    # Slabs: the closed polyline 12 000 000 and the circle 282 743.339 = 12 282 743.339; Walls: an open polyline and two
    # lines, no area; Empty: nothing; 0: one line.
    drawing: Path = request.getfixturevalue(f"plan_{extension}")

    process = _run("list_layers.py", drawing, cwd=work)

    assert process.returncode == 0, process.stderr
    assert process.stdout.splitlines() == [
        "layer\tobjects\tarea",
        "0\t1\t0.000",
        "Empty\t0\t0.000",
        "Slabs\t2\t12282743.339",
        "Walls\t3\t0.000",
        "total\t6\t12282743.339",
    ]


def test_list_layers_sorts_the_layers_by_name_without_regard_to_case(tmp_path: Path, work: Path) -> None:
    # "alpha" sorts before "Zeta" by (name.casefold(), name); a plain sort would put "Zeta" first (capitals come first).
    database = DxfDatabase.Parse(DxfWriter().ToString())
    for name in ("Zeta", "alpha"):
        layer = DxfLayerTableRecord()
        layer.Name = name
        database.LayerTable.Add(layer)
        database.ModelSpace.AppendEntity(_line((0, 0), (1, 0), name))
    path = tmp_path / "cased.dxf"
    database.Save(str(path))

    process = _run("list_layers.py", path, cwd=work)

    assert process.returncode == 0, process.stderr
    assert process.stdout.splitlines() == [
        "layer\tobjects\tarea",
        "0\t0\t0.000",
        "alpha\t1\t0.000",
        "Zeta\t1\t0.000",
        "total\t2\t0.000",
    ]


def test_list_layers_counts_an_object_on_a_layer_spelt_in_another_case_with_the_layer_it_is_on(
    tmp_path: Path, work: Path
) -> None:
    # Layer names are one name in whatever case (AutoCAD's, and the library's: DxfLayerTable finds "walls" as "Walls"),
    # yet an entity keeps the spelling it was written with. A line on "Walls" and a circle of radius 10 (area 314.159)
    # on "walls" are two objects on the one layer Walls, not a line on each of two.
    database = DxfDatabase.Parse(DxfWriter().ToString())
    walls = DxfLayerTableRecord()
    walls.Name = "Walls"
    database.LayerTable.Add(walls)
    circle = DxfCircle()
    circle.Center = GeoPoint3(0, 0, 0)
    circle.Radius = 10.0
    circle.Layer = "walls"
    database.ModelSpace.AppendEntity(_line((0, 0), (1, 0), "Walls"))
    database.ModelSpace.AppendEntity(circle)
    path = tmp_path / "spelt.dxf"
    database.Save(str(path))

    process = _run("list_layers.py", path, cwd=work)

    assert process.returncode == 0, process.stderr
    assert process.stdout.splitlines() == [
        "layer\tobjects\tarea",
        "0\t0\t0.000",
        "Walls\t2\t314.159",
        "total\t2\t314.159",
    ]


@pytest.mark.parametrize("example", ["list_layers.py", "coordinates_numpy.py"])
def test_an_example_that_reads_a_drawing_leaves_the_drawing_and_every_folder_as_they_were(
    plan_dxf: Path, work: Path, example: str
) -> None:
    before = (_sha256(plan_dxf), sorted(p.name for p in plan_dxf.parent.iterdir()))

    process = _run(example, plan_dxf, cwd=work)

    assert process.returncode == 0, process.stderr
    assert (_sha256(plan_dxf), sorted(p.name for p in plan_dxf.parent.iterdir())) == before
    assert list(work.iterdir()) == []


# ---- coordinates_numpy.py ----------------------------------------------------------------------------------------


def test_coordinates_numpy_prints_each_polyline_in_handle_order_and_the_extents_of_all_vertices(
    plan_dxf: Path, work: Path
) -> None:
    # The slab: 4 vertices, a perimeter of 4000 + 3000 + 4000 + 3000 = 14 000. The wall: 3 vertices, open, 1000 + 1000 =
    # 2000. The vertices run from (0, 0) to (4000, 5000). The handles are read from the file itself.
    polylines = [entity for entity in _objects(plan_dxf) if isinstance(entity, DxfPolyline)]
    handles = sorted((str(entity.Handle) for entity in polylines), key=lambda handle: int(handle, 16))
    slab, wall = handles

    process = _run("coordinates_numpy.py", plan_dxf, cwd=work)

    assert process.returncode == 0, process.stderr
    assert process.stdout.splitlines() == [
        f"{slab}\t4\t14000.000",
        f"{wall}\t3\t2000.000",
        "extents\t0.000,0.000\t4000.000,5000.000",
    ]


def test_coordinates_numpy_orders_the_handles_as_numbers_not_as_text(tmp_path: Path, work: Path) -> None:
    # Handles are hexadecimal text: 0xFFF is "FFF" and 0x1000 is "1000", which sorts before "FFF" as text. Two polylines
    # get those two handles (the lines before them only use the numbers up), and come in the order of the numbers:
    # (0, 0) (10, 0) of length 10, then (0, 5) (3, 5) (3, 9) of length 7.
    database = DxfDatabase.Parse(DxfWriter().ToString())
    padding = DxfLine(GeoPoint3(0, 0, 0), GeoPoint3(1, 0, 0))
    database.ModelSpace.AppendEntity(padding)
    while int(str(padding.Handle), 16) < 0xFFE:
        padding = DxfLine(GeoPoint3(0, 0, 0), GeoPoint3(1, 0, 0))
        database.ModelSpace.AppendEntity(padding)
    first = _polyline([(0, 0), (10, 0)], False, "0")
    second = _polyline([(0, 5), (3, 5), (3, 9)], False, "0")
    database.ModelSpace.AppendEntity(first)
    database.ModelSpace.AppendEntity(second)
    assert (str(first.Handle), str(second.Handle)) == ("FFF", "1000")
    path = tmp_path / "handles.dxf"
    database.Save(str(path))

    process = _run("coordinates_numpy.py", path, cwd=work)

    assert process.returncode == 0, process.stderr
    assert process.stdout.splitlines() == [
        "FFF\t2\t10.000",
        "1000\t3\t7.000",
        "extents\t0.000,0.000\t10.000,9.000",
    ]


# ---- make_drawing.py ---------------------------------------------------------------------------------------------


@pytest.mark.parametrize("extension", ["dxf", "dwg"])
def test_make_drawing_saves_a_rectangle_and_a_circle_on_a_layer_of_its_own(
    tmp_path: Path, work: Path, extension: str
) -> None:
    # A rectangle 4000 by 3000 (area 12 000 000) and a circle of radius 500 about (2000, 1500), both on Outline, which
    # the drawing has besides layer 0. OUT is the only file written.
    out_folder = tmp_path / "out"
    out_folder.mkdir()
    out = out_folder / f"outline.{extension}"

    process = _run("make_drawing.py", out, cwd=work)

    assert process.returncode == 0, process.stderr
    assert process.stdout.splitlines() == ["objects 2", "layers 0, Outline", f"saved outline.{extension}"]
    assert [p.name for p in out_folder.iterdir()] == [f"outline.{extension}"]
    assert list(work.iterdir()) == []
    objects = _objects(out)
    assert [str(entity.Layer) for entity in objects] == ["Outline", "Outline"]
    (polyline,) = [entity for entity in objects if isinstance(entity, DxfPolyline)]
    (circle,) = [entity for entity in objects if isinstance(entity, DxfCircle)]
    assert polyline.Closed
    assert polyline.Area == pytest.approx(12_000_000.0, abs=1e-3)
    assert (circle.Center.X, circle.Center.Y, circle.Radius) == pytest.approx((2000.0, 1500.0, 500.0), abs=1e-3)
    assert _layer_names(out) == ["0", "Outline"]


# ---- regions.py, solids.py ---------------------------------------------------------------------------------------


def test_regions_prints_the_areas_of_two_overlapping_squares_combined_and_offset(work: Path) -> None:
    # Squares A (0, 0)-(100, 100) and B (50, 50)-(150, 150), 10 000 each, overlapping in 50 by 50 = 2 500: union
    # 10 000 + 10 000 - 2 500 = 17 500; A minus B 7 500; A offset by +10 with mitred corners 120 by 120 = 14 400; by -10
    # 80 by 80 = 6 400.
    process = _run("regions.py", cwd=work)

    assert process.returncode == 0, process.stderr
    assert process.stdout.splitlines() == [
        "union 17500.000",
        "subtract 7500.000",
        "intersect 2500.000",
        "offset +10 14400.000",
        "offset -10 6400.000",
    ]


def test_solids_prints_the_volume_the_area_and_the_centre_of_a_box_with_a_hole_through_it(work: Path) -> None:
    # A box 1000 by 500 by 200 is 100 000 000 in volume and 2 (500 000 + 200 000 + 100 000) = 1 600 000 in area; a hole
    # 100 by 100 through its 200 takes 2 000 000 of volume and 2 x 10 000 of the top and bottom, and adds 4 x 100 x
    # 200 = 80 000 of wall: 98 000 000 and 1 660 000. The hole is on the middle line in x and y, so the centre stays
    # in the middle of the box.
    process = _run("solids.py", cwd=work)

    assert process.returncode == 0, process.stderr
    assert process.stdout.splitlines() == [
        "volume 98000000.000",
        "area 1660000.000",
        "centroid 500.000,250.000,100.000",
    ]


def test_solids_passes_its_tolerance_to_the_cut_and_does_not_read_the_global_one(work: Path) -> None:
    # The example run in a process of its own whose Tolerance.Global is 120 for points, wider than the hole's 100: a
    # cut that read the global tolerance loses the hole (measured: volume 100 000 000, area 1 600 000, the uncut box;
    # 60 still cuts it). Setting the global here is safe: it lives and dies with the child process.
    script = LIBRARY / "solids.py"
    code = (
        "import runpy, sys\n"
        "import geometryhelper\n"
        "from GeometryHelper import Tolerance\n"
        "Tolerance.Global = Tolerance(120.0, 1e-5)\n"
        "sys.argv = [sys.argv[1]]\n"
        "runpy.run_path(sys.argv[0], run_name='__main__')\n"
    )
    process = subprocess.run(
        [sys.executable, "-I", "-c", code, str(script)], capture_output=True, text=True, timeout=120, cwd=work
    )

    assert process.returncode == 0, process.stderr
    assert process.stdout.splitlines() == [
        "volume 98000000.000",
        "area 1660000.000",
        "centroid 500.000,250.000,100.000",
    ]


# ---- batch_folder.py ---------------------------------------------------------------------------------------------


@pytest.fixture
def folder(tmp_path: Path) -> Path:
    """The batch folder of D1006: a.dxf and b.dwg (S), broken.dxf (no drawing), notes.txt, and sub/ with a copy of S."""
    made = tmp_path / "plans"
    (made / "sub").mkdir(parents=True)
    _write_s(made / "a.dxf")
    _write_s(made / "b.dwg")
    (made / "broken.dxf").write_bytes(b"not a drawing\n")
    (made / "notes.txt").write_bytes(b"ignored\n")
    _write_s(made / "sub" / "inner.dxf")
    return made


def _rows(report: Path) -> list[list[str]]:
    data = report.read_bytes()
    assert b"\r" not in data, "the report has a CR: lines end with LF"
    assert data.endswith(b"\n")
    return list(csv.reader(data.decode("utf-8").splitlines()))


def test_batch_folder_reports_each_drawing_of_the_folder_and_goes_on_after_one_that_cannot_be_read(
    folder: Path, tmp_path: Path, work: Path
) -> None:
    # Three files are read (a.dxf, b.dwg, broken.dxf; not notes.txt and not the subfolder), each of S's 6 objects on 4
    # layers; the broken one is a row with its error, and the run ends with exit code 1 since one failed.
    report = tmp_path / "report.csv"

    process = _run("batch_folder.py", folder, report, cwd=work)

    assert process.returncode == 1, process.stderr
    assert process.stdout.splitlines() == ["3 files, 1 failed"]
    rows = _rows(report)
    assert rows[:3] == [
        ["file", "version", "objects", "layers", "error"],
        ["a.dxf", _version_of(folder / "a.dxf"), "6", "4", ""],
        ["b.dwg", _version_of(folder / "b.dwg"), "6", "4", ""],
    ]
    assert len(rows) == 4
    name, version, objects, layers, error = rows[3]
    assert (name, version, objects, layers) == ("broken.dxf", "", "", "")
    assert error.startswith(NOT_A_DXF)
    assert list(work.iterdir()) == []


def test_batch_folder_exits_with_zero_when_every_drawing_was_read(folder: Path, tmp_path: Path, work: Path) -> None:
    (folder / "broken.dxf").unlink()
    report = tmp_path / "report.csv"

    process = _run("batch_folder.py", folder, report, cwd=work)

    assert process.returncode == 0, process.stderr
    assert process.stdout.splitlines() == ["2 files, 0 failed"]
    assert [row[0] for row in _rows(report)] == ["file", "a.dxf", "b.dwg"]


def test_batch_folder_lists_the_files_without_regard_to_case_and_reads_the_one_after_a_broken_one(
    tmp_path: Path, work: Path
) -> None:
    # "a.dxf", "B.dxf" (broken) and "c.dxf" sort as a, B, c by (name.casefold(), name); a plain sort would put B first.
    # The file after the broken one is read all the same.
    plans = tmp_path / "plans"
    plans.mkdir()
    _write_s(plans / "c.dxf")
    (plans / "B.dxf").write_bytes(b"not a drawing\n")
    _write_s(plans / "a.dxf")
    report = tmp_path / "report.csv"

    process = _run("batch_folder.py", plans, report, cwd=work)

    assert process.returncode == 1, process.stderr
    assert process.stdout.splitlines() == ["3 files, 1 failed"]
    rows = _rows(report)
    assert [row[0] for row in rows[1:]] == ["a.dxf", "B.dxf", "c.dxf"]
    assert [row[4] == "" for row in rows[1:]] == [True, False, True]
    assert rows[3][2:4] == ["6", "4"]


def test_batch_folder_reports_a_dwg_cut_short_as_a_failed_row(tmp_path: Path, work: Path) -> None:
    # The first half of a real DWG: it starts as one and ends before its data does.
    plans = tmp_path / "plans"
    plans.mkdir()
    whole = _write_s(tmp_path / "whole.dwg").read_bytes()
    (plans / "cut.dwg").write_bytes(whole[: len(whole) // 2])
    report = tmp_path / "report.csv"

    process = _run("batch_folder.py", plans, report, cwd=work)

    assert process.returncode == 1, process.stderr
    assert process.stdout.splitlines() == ["1 files, 1 failed"]
    (_, row) = _rows(report)
    assert row[:4] == ["cut.dwg", "", "", ""]
    assert row[4] != ""
    assert "\n" not in row[4]


# ---- convert.py --------------------------------------------------------------------------------------------------


def _warnings_then(process: "subprocess.CompletedProcess[str]") -> str:
    """The last line of the output after checking that every line before it is a ``warning: <text>`` line."""
    lines = process.stdout.splitlines()
    assert lines, process.stderr
    assert all(line.startswith("warning: ") for line in lines[:-1]), lines
    return lines[-1]


def test_convert_writes_a_dxf_as_a_dwg_in_the_version_of_the_input(plan_dxf: Path, tmp_path: Path, work: Path) -> None:
    out = tmp_path / "plan-out.dwg"
    before = _sha256(plan_dxf)
    version = _version_of(plan_dxf)

    process = _run("convert.py", plan_dxf, out, cwd=work)

    assert process.returncode == 0, process.stderr
    assert _warnings_then(process) == f"wrote plan-out.dwg ({version})"
    assert _version_of(out) == version
    assert (len(_objects(out)), _layer_names(out)) == (6, S_LAYERS)
    assert _sha256(plan_dxf) == before
    assert list(work.iterdir()) == []


def test_convert_writes_a_dwg_as_a_dxf_in_the_version_of_the_input(plan_dwg: Path, tmp_path: Path, work: Path) -> None:
    out = tmp_path / "plan-out.dxf"
    version = _version_of(plan_dwg)

    process = _run("convert.py", plan_dwg, out, cwd=work)

    assert process.returncode == 0, process.stderr
    assert _warnings_then(process) == f"wrote plan-out.dxf ({version})"
    assert _version_of(out) == version
    assert (len(_objects(out)), _layer_names(out)) == (6, S_LAYERS)


@pytest.mark.parametrize("extension", ["dwg", "dxf"])
def test_convert_with_a_version_writes_that_version(plan_dxf: Path, tmp_path: Path, work: Path, extension: str) -> None:
    out = tmp_path / f"versioned.{extension}"

    process = _run("convert.py", plan_dxf, out, "--version", "AC1027", cwd=work)

    assert process.returncode == 0, process.stderr
    assert _warnings_then(process) == f"wrote versioned.{extension} (AC1027)"
    assert _version_of(out) == "AC1027"
    assert (len(_objects(out)), _layer_names(out)) == (6, S_LAYERS)


def test_a_drawing_converted_to_dwg_and_back_to_dxf_keeps_its_six_objects_on_four_layers(
    plan_dxf: Path, tmp_path: Path, work: Path
) -> None:
    middle, last = tmp_path / "middle.dwg", tmp_path / "last.dxf"

    first = _run("convert.py", plan_dxf, middle, cwd=work)
    second = _run("convert.py", middle, last, cwd=work)

    assert (first.returncode, second.returncode) == (0, 0), first.stderr + second.stderr
    assert (len(_objects(last)), _layer_names(last)) == (6, S_LAYERS)
    closed = [entity for entity in _objects(last) if isinstance(entity, DxfCircle) or getattr(entity, "Closed", False)]
    assert sorted(entity.Area for entity in closed) == pytest.approx([282743.3388230814, 12_000_000.0], abs=1e-3)


# ---- wrong arguments ---------------------------------------------------------------------------------------------


@pytest.mark.parametrize(
    ("example", "arguments"),
    [
        ("list_layers.py", []),
        ("list_layers.py", ["one.dxf", "two.dxf"]),
        ("coordinates_numpy.py", []),
        ("coordinates_numpy.py", ["one.dxf", "two.dxf"]),
        ("make_drawing.py", []),
        ("make_drawing.py", ["one.dxf", "two.dxf"]),
        ("batch_folder.py", []),
        ("batch_folder.py", ["folder"]),
        ("batch_folder.py", ["folder", "report.csv", "extra"]),
        ("convert.py", []),
        ("convert.py", ["in.dxf"]),
        ("convert.py", ["in.dxf", "out.dwg", "extra"]),
        ("convert.py", ["in.dxf", "out.dwg", "--version"]),
    ],
    ids=lambda value: " ".join(value) if isinstance(value, list) else value,
)
def test_an_example_given_the_wrong_arguments_says_how_to_use_it_and_exits_with_two(
    work: Path, example: str, arguments: list[str]
) -> None:
    process = _run(example, *arguments, cwd=work)

    assert process.returncode == 2, process.stdout + process.stderr
    assert process.stdout == ""
    assert process.stderr.lower().startswith("usage")
    assert "Traceback" not in process.stderr
    assert list(work.iterdir()) == []

