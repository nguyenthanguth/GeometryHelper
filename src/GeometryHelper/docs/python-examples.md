# Python examples

Thirteen scripts, each doing one thing people do with GeometryHelper from Python: list the layers of a drawing, make a
drawing in code, cut one solid from another, number points in the app, draw in an app that is running. They are in
`src/python/examples` of the repository, in three folders for the three places Python works with the library, and this page
shows each file as it is, with what it prints. A test of the repository runs every file, on .NET 8 and on .NET Framework
4.8, against a drawing the test builds, and a type check (pyright) finds no error in any of them, so what the page shows
is what the files do.

| Folder | Where it runs | How a file is run |
|---|---|---|
| `library` | outside the app, on the libraries alone | `python list_layers.py plan.dxf`, in the virtual environment the wheel is installed in ([Installing](python.md#installing)) |
| `in_app` | inside GeometryHelper CAD | PYRUN runs a file and PYLOAD loads one ([Python commands and tools](presenter/python.md)) |
| `remote` | outside the app, driving one that is running | `python draw_and_read.py` while the app runs with "Allow other programs to connect" on ([the setting](presenter/remote.md#the-setting)) |

| File | What it does |
|---|---|
| [`list_layers.py`](#list-the-layers-of-a-drawing) | the objects and the closed area on each layer of a DXF or DWG |
| [`coordinates_numpy.py`](#the-vertices-of-the-polylines-in-numpy) | every polyline's vertices as a numpy array, and the extents of all of them |
| [`make_drawing.py`](#make-a-drawing-in-code) | a layer, a rectangle and a circle added to a new drawing, saved as DXF or DWG |
| [`regions.py`](#union-difference-and-offset-of-regions) | union, difference, intersection and offset of two squares |
| [`solids.py`](#a-box-with-a-hole-measured) | a box with a hole cut through it: volume, surface area and centroid |
| [`batch_folder.py`](#a-csv-report-of-a-folder) | one CSV row for each DXF and DWG file of a folder |
| [`convert.py`](#convert-between-dxf-and-dwg) | a drawing read and written again as DXF or DWG, in a version |
| [`number_points.py`](#number-the-points-you-pick) | a number written at each point you pick |
| [`drawing_tools.py`](#commands-of-your-own) | the commands RECTC and LAYERCOUNT |
| [`selection_lengths.py`](#the-length-of-a-selection) | the length and closed area of the selected objects |
| [`draw_and_read.py`](#draw-a-grid-and-read-it-back) | a grid drawn in a new drawing of the running app, read back |
| [`pick_and_mark.py`](#pick-a-point-and-mark-it) | a circle drawn where the user picks |
| [`open_documents.py`](#the-open-drawings) | the drawings the app has open and what is in each |

Every file follows the same plan. The ones that take arguments are a `main(argv)` that prints a usage line to the standard
error and returns 2 when the arguments are wrong, and ends in `sys.exit(main(sys.argv[1:]))`. Numbers are printed with three
decimals, in the drawing's own units. Where a script compares or offsets geometry it passes a
[`Tolerance`](xref:GeometryHelper.Tolerance) (`Tolerance.Default`, a thousandth of a millimetre for points). The numbers
below were printed by the files on a drawing of four layers and six objects that the tests build, called the test drawing
here: layers `0`, `Empty`, `Slabs` and `Walls`; on `0` a line of 100; on `Slabs` a closed polyline 4 000 by 3 000 and a
circle of radius 300 at its middle; on `Walls` an open polyline of two segments of 1 000 and two lines of 500; `Empty`
holds nothing. The same drawing saved as DXF and as DWG gives the same lines on both runtimes.

## Outside the app

### List the layers of a drawing

`list_layers.py DRAWING` reads a DXF or DWG file with `open_file` and prints one line for each layer of its layer table, sorted by name, with
the number of objects of model space on it and the area enclosed by the closed polylines and circles on it.

```text
layer	objects	area
0	1	0.000
Empty	0	0.000
Slabs	2	12282743.339
Walls	3	0.000
total	6	12282743.339
```

The area of `Slabs` is 12 000 000 for the polyline and 282 743.339 for the circle (pi times 300 squared). The open polyline
of `Walls` encloses nothing, and `list_layers.py` counts it as an object only. A DWG gives the same lines as the DXF. A
layer name is one name in whatever case, while an object keeps the spelling it was written with, so a line on `Walls` and a
circle of radius 10 on `walls` are counted together under the layer table's spelling: `Walls` with 2 objects and an area
of 314.159.

[!code-python[](../../python/examples/library/list_layers.py)]

### The vertices of the polylines in numpy

`coordinates_numpy.py DRAWING` reads the file with `open_file`, takes every polyline of model space in the order of its handles, and copies its vertices
into a numpy array of shape (n, 2) through [`geometryhelper.arrays`](python.md#numpy-in-blocks), which takes a whole .NET
array in one copy where a loop over the points would call into .NET three times a point. It needs numpy
(`pip install geometryhelper[numpy]`).

```text
104	4	14000.000
106	3	2000.000
extents	0.000,0.000	4000.000,5000.000
```

Each line is a polyline's handle, its vertex count and its length: the closed polyline is four corners with a perimeter of
14 000, the open one three vertices with a length of 2 000. The last line is the extents of all the vertices, taken with
numpy. A closed polyline's corners come from `ToGeoPolygon2`, since `ToGeoPolyline2` repeats the first corner at the end
and would give five.

[!code-python[](../../python/examples/library/coordinates_numpy.py)]

### Make a drawing in code

`make_drawing.py OUT` makes a blank drawing with `new_file()`, adds a layer `Outline` and makes it current, adds a rectangle
from 0,0 to 4000,3000 and a circle of radius 500 at 2000,1500 on it, applies them with one `commit()`, and saves the drawing
as `OUT`, DXF or DWG by its extension.

```text
objects 2
layers 0, Outline
saved plan.dwg
```

Opened again, the file holds a polyline of area 12 000 000 and a circle of radius 500 on `Outline`, as DXF and as DWG.

[!code-python[](../../python/examples/library/make_drawing.py)]

### Union, difference and offset of regions

`regions.py` takes two squares of 100, A from 0,0 and B from 50,50, and prints the area of the union, of A less B, of what
they share, and of A grown and shrunk by 10 with mitred corners (`OffsetJoin.Miter`).

```text
union 17500.000
subtract 7500.000
intersect 2500.000
offset +10 14400.000
offset -10 6400.000
```

The squares share 50 by 50, 2 500, so the union is 10 000 plus 10 000 less 2 500. A grown to 120 by 120 is 14 400, shrunk
to 80 by 80 6 400. Each call comes back as an array of faces or polygons, and the script adds their areas.

[!code-python[](../../python/examples/library/regions.py)]

### A box with a hole measured

`solids.py` makes a box 1 000 by 500 by 200 and cuts a hole of 100 by 100 through it with a second box that sticks out
of both faces by 100, so that no face of the cutter lies on one of the box, and prints the volume, the surface area and the
centroid of what is left.

```text
volume 98000000.000
area 1660000.000
centroid 500.000,250.000,100.000
```

The volume is the box's 100 000 000 less 2 000 000 for the hole, 100 by 100 by 200. The area is the box's 1 600 000, less
the two openings of 10 000 each, plus the four walls of the hole at 100 by 200 each, 80 000. The measures and the cut take
`Tolerance.Default`. `TrySubtract` takes it by name, `plate.TrySubtract(cutter, tolerance=tolerance)`, because a parameter
that comes after an out parameter is keyword-only in Python, so the script gives the same numbers whatever
`Tolerance.Global` is: run with a global tolerance of 120, wider than the hole, a cut that read the global one would leave
the box uncut, 100 000 000 and 1 600 000.

[!code-python[](../../python/examples/library/solids.py)]

### A CSV report of a folder

`batch_folder.py FOLDER REPORT.csv` opens every `.dxf` and `.dwg` file of the folder, not those of its subfolders, in the
order of their names, and writes one row each to a CSV file: the file, its version, the objects of model space, the layers,
and an error. A file that cannot be read is a row with its error and the next one is read. The script prints how many files
there were and how many failed, and exits with 1 when any failed.

Run on a folder with the test drawing as `a.dxf` and `b.dwg`, a file `broken.dxf` that holds the words `not a drawing`, a
text file and a subfolder with another drawing, it prints

```text
3 files, 1 failed
```

and writes

```text
file,version,objects,layers,error
a.dxf,AC1015,6,4,
b.dwg,AC1032,6,4,
broken.dxf,,,,"This is not a DXF that can be read: line 1 should be a group code, a whole number, and is 'not a drawing'."
```

The version is the one the file says it is: a drawing made in code with `DxfWriter` is AC1015 (AutoCAD 2000), and
`DwgWriter` writes AC1032 (AutoCAD 2018) unless it is told another. The `except Exception` takes the .NET exception that a file which is no drawing
raises; its first line is the error.

[!code-python[](../../python/examples/library/batch_folder.py)]

### Convert between DXF and DWG

`convert.py IN OUT [--version ACnnnn]` reads a DXF or DWG and writes `OUT` as a DXF or DWG by its extension, in the version
given or, where none is, that of `IN`. The DXF writer returns what it could not keep in the version, and each is printed
first as a line `warning: ...`; then the file written and its version.

```text
$ python convert.py plan.dxf plan.dwg
wrote plan.dwg (AC1015)
$ python convert.py plan.dwg plan2.dxf --version AC1024
wrote plan2.dxf (AC1024)
```

The test drawing converted from DXF to DWG and from there to DXF again keeps its six objects on four layers. A version the
writer does not write, AC1009 (R12) for a DWG, is a line on the standard error, `error: ...`, and the exit code 1. A drawing
with an MText converted to AC1009 prints `warning: Written as the lines, texts and polylines each breaks into, which AC1009
holds: MTEXT 1.` The DWG writer says nothing of what it leaves out: it logs it.

[!code-python[](../../python/examples/library/convert.py)]

## Inside the app

These files are run in GeometryHelper CAD with PYRUN and PYLOAD, and have no `main`: PYRUN runs the file, and PYLOAD runs it
once to find the functions marked `@command`. They use [`geometryhelper.cad`](presenter/python.md) for the prompts and the
`Drawing` API, and each run is one undo step, so one `U` takes back all it drew.

### Number the points you pick

`number_points.py`, run with PYRUN, asks `First number <1>:` and then `Specify point:` until Enter, and adds a text of
height 2.5 at each point with `add_text`, the numbers counting up; the texts are applied when the run ends. After a first number of 1 and three points it writes

```text
3 points numbered, 1 to 3.
```

The three texts read 1, 2 and 3 at the points picked. Enter at the first question and at the first point writes
`No point picked.` and draws nothing.

[!code-python[](../../python/examples/in_app/number_points.py)]

### Commands of your own

`drawing_tools.py`, loaded with PYLOAD, defines two commands. RECTC (alias RC) asks for a centre, a width (`<1000>`) and a
height (`<500>`) and draws a closed polyline rectangle about the centre; LAYERCOUNT writes the number of objects on each layer
that has any, sorted by name. PYLOAD writes

```text
drawing_tools.py loaded: LAYERCOUNT, RECTC.
```

RECTC with the centre 0,0, a width of 200 and a height of 100 draws a rectangle from -100,-50 to 100,50; with Enter at both
questions it is 1 000 by 500 about the centre. RECTC adds the rectangle with `add_polyline(..., closed=True)` and writes nothing itself, except for a width or a height of 0 or
less, which it refuses with `The width and the height must be more than 0.` and draws nothing (a rectangle of no width is a
polyline with nothing inside it). On a drawing with one rectangle on layer `0`, LAYERCOUNT writes
`0: 1`; an object on `walls` counts with the layer the table calls `Walls`.

[!code-python[](../../python/examples/in_app/drawing_tools.py)]

### The length of a selection

`selection_lengths.py`, run with PYRUN, asks `Select objects:` and adds up the length of the lines, arcs, circles (their
circumference) and polylines, and the area of the circles and closed polylines. Anything else is counted as skipped. On a
line from 0,0 to 300,400, a circle of radius 120, a closed polyline rectangle 100 by 50 and a text, all selected, it writes

```text
4 objects: length 1553.982, closed area 50238.934, 1 skipped
```

The length is the line's 500, the circle's 753.982 and the rectangle's 300; the area the circle's 45 238.934 and the
rectangle's 5 000. With nothing selected it writes `Nothing selected.`

[!code-python[](../../python/examples/in_app/selection_lengths.py)]

## Driving a running app

These files connect to GeometryHelper CAD from another process with [`CadApplication`](xref:GeometryHelper.Cad.Remote.CadApplication),
so the app has to be running with "Allow other programs to connect" on ([the setting](presenter/remote.md#the-setting)). Each
has a `main` that works on the connection it is given, wrapped in the `Drawing` API with `App(connection)` (what `connect()` gives),
and a `__main__` block that makes the connection with `with CadApplication() as app:`, which closes it at the end. [From Python](presenter/remote.md#from-python) tells how
the .NET calls look in Python. The numbers below come from the test server of the repository, an app with no window.

### Draw a grid and read it back

`draw_and_read.py [OUT]` starts a new drawing, adds a layer `Grid` and 11 vertical and 11 horizontal lines over a
square of 1 000 at a spacing of 100, commits them as one undo step, reads them back with `drawing.entities("LINE", layer="Grid")`
and, when `OUT` is given, saves the drawing.

```text
22 lines on Grid, total length 22000.000
saved grid.dxf
```

[!code-python[](../../python/examples/remote/draw_and_read.py)]

### Pick a point and mark it

`pick_and_mark.py` asks the user for a point (`Pick the centre`) and waits up to 60 seconds, then draws a circle of radius 50
there and zooms to the extents. The pick is made on `drawing.net`, the `CadRemoteDocument`, because `PickPoint` there takes the time limit that `drawing.pick_point` does not. A point of 120,80, clicked or typed on the app's command line, prints

```text
circle at 120.000,80.000
```

and the script returns 0. Esc in the app throws a `CadRemoteException` whose `IsCancelled` is true; the script prints
`Cancelled.` and returns 1. Any other exception, a timeout among them, is raised again. The time limit is there because
Ctrl+C does not end a call into .NET that waits for the user ([From Python](presenter/remote.md#from-python)).

[!code-python[](../../python/examples/remote/pick_and_mark.py)]

### The open drawings

`open_documents.py` lists the drawings the app has open, in the app's order: the name, with ` *` after it for the drawing the
user is working in, the number of objects in model space, and the count of each class of object, the classes sorted by name.
With a drawing of two lines and a circle and a new, empty one shown on top it prints, tab-separated,

```text
Drawing1.dxf	3	DxfCircle:1, DxfLine:2
Drawing2.dxf *	0	
```

[!code-python[](../../python/examples/remote/open_documents.py)]
