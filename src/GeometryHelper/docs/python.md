# Python

The library and the presenter from Python: geometry, DXF and DWG read and written, and a
[`CadDocument`](xref:GeometryHelper.Cad.CadDocument) edited through its command session and saved, with no window. It is
the .NET library itself, run by [pythonnet](https://pythonnet.github.io/), in a package named `geometryhelper`. The .NET
names stay as they are, so a script reads like the C# of the other pages; the package adds only the few Python helpers
that pythonnet makes slow or easy to get wrong, and type stubs, so that an editor completes and checks every name.

The package is not on PyPI yet. It is a wheel built from the repository ([Installing](#installing)), and the name it
will be published under is not chosen: `geometryhelper` is taken on PyPI by an unrelated project, so a wheel called so
can never be uploaded. The name to `import` stays `geometryhelper` whichever name is chosen.

Nothing is converted and no tolerance is chosen for you. Lengths are the library's own, in millimetres when the
drawing is, and a script passes a [`Tolerance`](xref:GeometryHelper.Tolerance) where the C# would.

## What it needs

| | |
|---|---|
| System | Windows, 64-bit. The two assemblies of the presenter are built for x64 and .NET Framework is Windows only, so the wheel is tagged `py3-none-win_amd64` and `pip` on any other system refuses it |
| Python | CPython 3.11 to 3.15, 64-bit (pythonnet 3.2.1's range) |
| .NET | the .NET 8 runtime or later, or .NET Framework 4.8; either runs the same script ([Choosing the runtime](#choosing-the-runtime)) |
| Installed with it | `pythonnet` 3.2.1 or later (below 4); numpy 1.26 or later when the `numpy` extra is asked for |

Linux, macOS and Windows on ARM are not supported: nothing has been run there.

## Installing

Build the wheel from a checkout of the repository, in Git Bash, from its root. The first two lines build the libraries
and the stub generator, the last two write the stubs and the wheel:

```bash
dotnet build src/GeometryHelper.Cad/GeometryHelper.Cad.csproj -c Release
dotnet build tools/GeometryHelper.PyStubs/GeometryHelper.PyStubs.csproj -c Release
bash tools/python/make-stubs.sh
bash tools/python/build-wheel.sh
```

The wheel is `artifacts/python/geometryhelper-13.12.0-py3-none-win_amd64.whl` (the version is the library's, the one
`Directory.Build.props` carries, so a NuGet package and a wheel of one number hold the same code). Install it into a
virtual environment, with the `numpy` extra if you want [the array helpers](#numpy-in-blocks):

```
python -m venv .venv
.venv\Scripts\python -m pip install "artifacts\python\geometryhelper-13.12.0-py3-none-win_amd64.whl[numpy]"
```

The wheel is 10 200 681 bytes. It holds the three assemblies (`GeometryHelper.dll`, `GeometryHelper.Cad.dll` and
`GeometryHelper.Cad.Remote.dll`) with the `Fonts`, `Hatches` and `Linetypes` folders the presenter finds beside them (8
files, 15 723 467 bytes unpacked), the Python module (6 files, 30 652 bytes), and the stubs (18 files, 4 449 463 bytes
for `GeometryHelper`, 8 files, 8 930 bytes for `System`). It holds no XML documentation and no PDB: the XML is read once,
when the stubs are written.

`bash tools/python/verify-python.sh` does all of it, then makes a fresh virtual environment, installs the wheel there and
runs `tests/python` against it, once on each runtime; the `python` job of `.github/workflows/build.yml` runs the same
script on Python 3.11 and 3.14, and then the tests of the Python inside the app on both runtimes (`GH_PYTHON` names
the Python to start). On the test machine, 158 tests pass on .NET 8 (27.2 s) and 158 on .NET Framework 4.8 (27.8 s).

## A first script

```python
import geometryhelper
from GeometryHelper.Cad import CadCommandSession, CadDocument
from GeometryHelper.Dxf import DxfDatabase, DxfWriter
from GeometryHelper.Geometry import GeoPoint3

print(geometryhelper.__version__, geometryhelper.runtime())      # 13.12.0 coreclr
print(GeoPoint3(0, 0, 0).DistanceTo(GeoPoint3(3, 4, 0)))         # 5.0

document = CadDocument.From(DxfDatabase.Parse(DxfWriter().ToString()))   # an empty drawing
session = CadCommandSession(document)                                     # its command line
with geometryhelper.undo_group(document, "A circle"):                     # one undo step
    for answer in ("CIRCLE", "0,0", "25"):                                # what a user types
        session.Feed(answer)
print(document.EntityCount)                                               # 1

document.SaveAs("circle.dwg")                                    # DWG, by the extension; .dxf writes a DXF
print(CadDocument.Open("circle.dwg").EntityCount)                # 1
```

`import geometryhelper` comes first: it starts .NET and loads the three assemblies, and only after it can
`from GeometryHelper.Geometry import ...` find anything (before it, the import fails with `ModuleNotFoundError: No module
named 'GeometryHelper'`). The drawing is read, edited and saved as the pages on [DXF](dxf/index.md),
[DWG](dxf/dwg.md) and [the 2D presenter](presenter/index.md) describe; this page is about what differs from C#.

`import geometryhelper` takes 498 to 512 ms on .NET 8 and 562 to 589 ms on .NET Framework 4.8 (three runs of the whole
process, which includes starting Python: a bare `python -c pass` takes 56 ms). A script that reads a DXF and a DWG, draws,
edits, saves both and reads geometry into numpy takes 927 ms on .NET 8 and 1 257 ms on .NET Framework 4.8, whole process.

`geometryhelper.__version__` is the version of the library, and `geometryhelper.runtime()` is `"coreclr"` or `"netfx"`,
the runtime the script runs on: what a bug report needs.

## Choosing the runtime

The environment variable `GEOMETRYHELPER_RUNTIME` says which .NET runs the script. It is read once, when
`geometryhelper` is imported, and its case is ignored:

| Value | Runs on |
|---|---|
| unset, empty or `auto` | .NET 8 or later when one is installed, else .NET Framework 4.8 |
| `coreclr` | .NET 8 or later; an `ImportError` when none is installed |
| `netfx` | .NET Framework 4.8 |

Any other value is an `ImportError`: `GEOMETRYHELPER_RUNTIME is 'bogus': use auto, coreclr or netfx.` With `coreclr` and
no .NET 8, it says `GEOMETRYHELPER_RUNTIME is coreclr but no .NET 8 or later is installed: install the .NET 8 runtime from
https://dotnet.microsoft.com/download or set GEOMETRYHELPER_RUNTIME=netfx.` The package does not read `PYTHONNET_RUNTIME`.

On .NET the package asks for 8.0 and lets the runtime roll forward to the lowest major installed from 8 up, at its latest
patch. On a machine with 8.0.11, 8.0.28, 9.0.17, 10.0.0, 10.0.9 and 10.0.13 that is 8.0.28. To run on the newest
installed instead, set the .NET variable `DOTNET_ROLL_FORWARD=LatestMajor`: the same machine then runs 10.0.13, because
that variable overrides the package's own setting.

When .NET is already running (the program called `pythonnet.load` itself, or it embeds Python and has `clr` imported),
the package starts nothing and uses it, so `GEOMETRYHELPER_RUNTIME` is not read. Of its three assemblies, one whose name is
already loaded is used as it is, not loaded a second time, so a host's own copy stays the only one.

## The .NET names stay as they are

A script imports the .NET namespaces as they are written in C#, and uses members as C# does:

```python
import geometryhelper
from GeometryHelper.Geometry import GeoPoint3, GeoPolygon2, GeoPoint2

p = GeoPoint3(x=1, y=2, z=3)           # arguments by their .NET names work
q = GeoPoint3(4, 6, 3)
print(q - p, (q - p).Length)           # [3, 4, 0] 5.0: operators work, a property takes no parentheses
print(p.DistanceTo(q))                 # 5.0
print(GeoPolygon2(GeoPoint2(0, 0), GeoPoint2(5, 0), GeoPoint2(5, 4)).Area)   # 10.0
```

The namespaces are `GeometryHelper`, `GeometryHelper.Geometry`, `.Dxf`, `.Dwg`, `.Cad`, `.Cad.Remote` and the others of
the [API reference](xref:GeometryHelper). A few things differ from C# at run time:

- A struct has no parameterless constructor: `GeoPoint3()` is a `TypeError`. Write `GeoPoint3(0, 0, 0)`.
- `str(p)` is the library's `ToString()`, `(1, 2, 3)`; `repr(p)` is the generic one. `GeoPoint3.DistanceTo.__doc__` lists
  the overload signatures and nothing else: pythonnet does not read the XML documentation, which is why the stubs carry it
  ([Stubs and an editor](#stubs-and-an-editor)).
- An enum value is a .NET object: `int(CadSupportKind.Fonts)` is 0 and `str(CadSupportKind.Fonts)` is `Fonts`, but
  `CadSupportKind.Hatches == 1` is False. Compare enum with enum, or take `int(...)` of both.
- Exceptions are the .NET ones, with the .NET message and stack trace in `str(error)`.

## What pythonnet does with .NET

Six things differ from what a Python reader expects. Each was measured on .NET 8 and on .NET Framework 4.8, with the same
result, and the stubs are written to say them: a call that pythonnet would refuse is an error in the editor.

### Out parameters come back in a tuple

A .NET method with an `out` parameter returns a tuple of its result and the out values, and the out parameters are left
out of the call:

```python
from GeometryHelper.Geometry import GeoLine2, GeoPoint2

crossing = GeoLine2(GeoPoint2(0, 0), GeoPoint2(10, 0))
ok, point = crossing.TryIntersectWith(GeoLine2(GeoPoint2(5, -5), GeoPoint2(5, 5)))   # True, (5, 0)
ok, point = crossing.TryIntersectWith(GeoLine2(GeoPoint2(20, -5), GeoPoint2(20, 5))) # False, (0, 0)
```

When the call fails the out value is the default of its type, as in C# (the point above is `(0, 0)`), so test the first
item before using the others.

### Lists: only for arrays

A Python `list` or `tuple` is accepted where the .NET parameter is an array (`T[]`), and where it is a `params T[]`, which
also takes separate arguments. Nowhere else: not for `IEnumerable<T>`, `IReadOnlyList<T>`, `IReadOnlyCollection<T>`,
`IList<T>`, `ICollection<T>`, `List<T>` or `ISet<T>`, and a generator is accepted nowhere (make a `list` of it first).

```python
from System import Array
from System.Collections.Generic import List
from GeometryHelper.Geometry import GeoPoint2, GeoPolygon2, GeoRectangle2

a, b, c = GeoPoint2(0, 0), GeoPoint2(5, 0), GeoPoint2(5, 4)
GeoPolygon2(a, b, c).Area              # 10.0: a params array takes separate arguments
GeoPolygon2([a, b, c]).Area            # 10.0: or a list

GeoRectangle2.Fit([a, b, c])           # TypeError: No method matches given arguments for GeoRectangle2.Fit: (<class 'list'>)
GeoRectangle2.Fit(Array[GeoPoint2]([a, b, c])).Area    # 20.0: Fit takes an IEnumerable<GeoPoint2>

points = List[GeoPoint2]()             # List[GeoPoint2]([a, b, c]) is refused too
for point in (a, b, c):
    points.Add(point)
GeoRectangle2.Fit(points).Area         # 20.0
```

So for a collection interface, build an `Array[T]` (it takes a Python list) or a `List[T]` (add one by one). A .NET array
is accepted for `IEnumerable<T>`, `IReadOnlyList<T>`, `IReadOnlyCollection<T>` and `IList<T>`, and a `List[T]` for all of
them; an array is not accepted for a parameter of type `List<T>`. A nested `IEnumerable<IEnumerable<T>>` takes no Python
list of lists either.

Ten members take a `params` array, among them the constructors of `GeoPolygon2`, `GeoPolygon3`, `GeoPolyline2`,
`GeoPolyline3` and `GeoSolid3`. The stubs show each as an overload with a list and an overload with `*points`.

### `len` and `.Count`

What `len()` works on depends on the .NET type the object is wrapped as, and a list a method returns is wrapped as the
interface it declares:

| Object | `len(x)` | `x.Count` | `x[-1]` |
|---|---|---|---|
| a .NET array, `Array[GeoPoint2]` | works | no | the last item |
| a `List[T]`, built or returned | works | works | `ArgumentOutOfRangeException` |
| an `IReadOnlyList[T]`, such as `GeoPolygon2.Vertices` | `TypeError` | works | `ArgumentOutOfRangeException` |
| a class that only iterates, such as a drawing's model space | `TypeError` | works on the model space, not on every such class | not subscriptable |

An `IReadOnlyList` has an index from 0, iteration, `in` and `.Count`, and no slice. A negative index is not
`IndexError` but a .NET exception. Where you need a Python list, `list(polygon.Vertices)` is one.

```python
vertices = GeoPolygon2(GeoPoint2(0, 0), GeoPoint2(5, 0), GeoPoint2(5, 4)).Vertices
vertices.Count            # 3
len(vertices)             # TypeError: object of type 'IReadOnlyList[GeoPoint2]' has no len()
vertices[2]               # (5, 4)
```

### Delegates are built, events are not

A parameter or a property of a delegate type (`Action<...>`, `Func<...>` and the delegates of the plugins) does not take a
bare Python function. Build the delegate from it:

```python
import System
from System import Action
from GeometryHelper import GeometryHelperLog
from GeometryHelper.Enums import GeometryHelperLogLevel

messages: list[tuple[str, str]] = []

def write(level: GeometryHelperLogLevel, text: str, error: System.Exception) -> None:
    messages.append((str(level), text))

GeometryHelperLog.Writer = lambda level, text, error: None      # TypeError: 'function' value cannot be converted ...
GeometryHelperLog.Writer = Action[GeometryHelperLogLevel, str, System.Exception](write)   # works
GeometryHelperLog.Warn("hello")                                 # messages == [("Warn", "hello")]
```

Assigning `None` puts the default writer back. The stubs do not model null yet, so an editor flags that one assignment;
the script runs.

An event is the exception: `+=` takes a plain function, with the .NET handler's two arguments, and `-=` takes it away:

```python
def on_changed(sender, args):
    print(args.Kind, args.StepName)

document.Changed += on_changed          # one CIRCLE command prints: Edit CIRCLE
document.Changed -= on_changed
```

Assigning a static property works as in C#: `GeometryHelperLog.Enable = False`. The library's settings are shared by the
whole process, here as there; `Tolerance.Global` is one, so a script that sets it sets it for everything it imports.

### Members named `None`, `True` or `False`

Python reads `DxfEndCap.None` as a syntax error, though the library has such a member. Read it with `getattr`:

```python
from GeometryHelper.Dxf import DxfEndCap

no_cap = getattr(DxfEndCap, "None")      # int(no_cap) is 0; int(DxfEndCap.Round) is 1
```

The library has 33 members so named: 29 `None`, 2 `True` and 2 `False`. The stubs leave them out and the generator lists
them, so an editor does not offer them and a `getattr` returns `Any`. A parameter named `from`, `with` or `except` (14
of them) is written `from_` in the stubs and made positional-only, because that is the way pythonnet takes it:
`line.ToLine(0.0, 10.0)` works, `line.ToLine(from_=0.0, to=10.0)` is a `TypeError`, and the editor says so first.

### Flags operators on `[Flags]` enums only

The 14 `[Flags]` enums combine with `|`, `&`, `^` and `~`, each between values of the same enum:

```python
from GeometryHelper.Dxf import DxfEraseFlags

both = DxfEraseFlags.ZeroLengthCurve | DxfEraseFlags.EmptyText      # str(both): ZeroLengthCurve, EmptyText
int(both)                                                           # 3
(both & DxfEraseFlags.ZeroLengthCurve) == DxfEraseFlags.ZeroLengthCurve   # True
int(~DxfEraseFlags.ZeroLengthCurve)                                 # -2
database.CountEmptyObjects(int(both))                               # a method that takes the number, not the enum
```

Two traps. `bool(a & b)` is True even when `a & b` is the zero value, so test `int(a & b) != 0`. And `|` on an enum that is
not `[Flags]`, such as `DxfEndCap.Round | DxfEndCap.Square`, does not raise a `TypeError`: pythonnet recurses until it
raises `RecursionError`. The stubs give the operators to the `[Flags]` enums only, so an editor reports the second case
before it runs. A flag and a plain number do not combine either: `DxfEraseFlags.ZeroLengthCurve | 1` is refused at run time
and in the editor alike, so combine the named values.

## numpy in blocks

Reading a point array from Python one property at a time, `p.X`, `p.Y`, `p.Z` of each element, crosses into .NET three
times a point. `geometryhelper.arrays` copies a whole array in one block instead: it pins the .NET array and copies it
with one `memmove`. It needs numpy (`pip install "<wheel>[numpy]"`); without it `import geometryhelper.arrays` is an
`ImportError`: `geometryhelper.arrays needs numpy: pip install geometryhelper[numpy]`.

| Function | Takes | Gives |
|---|---|---|
| `to_numpy(values)` | a .NET array, or an `IEnumerable<T>`, of `Double`, `GeoPoint2` or `GeoPoint3` | a new numpy array, float64: shape `(n,)`, `(n, 2)` or `(n, 3)` |
| `to_doubles(array)` | a 1-D array-like, any length, 0 included | an `Array[Double]` |
| `to_points2(array)` | an array-like of shape `(n, 2)` | an `Array[GeoPoint2]` |
| `to_points3(array)` | an array-like of shape `(n, 3)` | an `Array[GeoPoint3]` |

Every copy is bit for bit the source. Another shape is a `ValueError` naming the shape expected and the one given
(`to_points3 expects shape (n, 3), got (4, 2).`), and `to_numpy` of a Python list or of another element type is a
`TypeError` naming what it takes. NaN and infinities are copied as they are; the library decides what to do with them.
A collection that is not an array, such as `GeoPolygon2.Vertices`, goes through one `Enumerable.ToArray` call first.

```python
import numpy as np
from geometryhelper.arrays import to_numpy, to_points2
from GeometryHelper.Geometry import GeoPolygon2

rectangle = np.array([[0.0, 0.0], [40.0, 0.0], [40.0, 30.0], [0.0, 30.0]])    # 40 by 30
polygon = GeoPolygon2(to_points2(rectangle))      # an Array[GeoPoint2], so no list of points is built by hand
polygon.Area                                      # 1200.0
corners = to_numpy(polygon.Vertices)              # shape (4, 2), equal to `rectangle`
```

For 100 000 points, on .NET 8 and on .NET Framework 4.8:

| Step | .NET 8 | .NET Framework 4.8 |
|---|---|---|
| numpy `(n, 3)` to `GeoPoint3[]` | 3.1 ms | 4.3 ms |
| `GeoPoint3[]` to numpy | 0.8 ms | 0.4 ms |
| `GeoPolygon2.Vertices` to numpy (`ToArray`, then the copy) | 3.2 ms | 4.4 ms |
| `GeoPoint3[]` to numpy, reading `X`, `Y` and `Z` of each point | 283 ms | 202 ms |

The block copy of `GeoPoint3[]` to numpy is 350 to 500 times faster than reading the properties. It rests on `GeoPoint2` and `GeoPoint3` being structs of exactly two and three
doubles (16 and 24 bytes on both runtimes); a test pins that, and there is no fallback if it ever changed. Vectors are not
offered: nobody has asked for them.

## Undo groups

An edit of a [`CadDocument`](xref:GeometryHelper.Cad.CadDocument) made through its command session is one undo step per
command. `BeginGroup(name)` and `EndGroup()` make several of them one; `CancelGroup()` takes the group back. The pair has to
stay balanced: a group left open by an exception makes the next `SaveAs` throw `InvalidOperationException: A command is
running: end it before saving.` `geometryhelper.undo_group` keeps it balanced:

```python
from geometryhelper import undo_group

with undo_group(document, "Two circles"):
    for answer in ("CIRCLE", "0,0", "25", "CIRCLE", "100,0", "10"):
        session.Feed(answer)
document.EntityCount        # 2
document.Undo()             # True: both circles go in one step
document.EntityCount        # 0
```

`BeginGroup(name)` runs on entry. `EndGroup()` runs when the block ends normally. `CancelGroup()` runs when it raises, the
edits made in the block are taken back, and the exception goes on: in a drawing of one circle, a block that fed a
second one and then raised `ValueError("boom")` leaves one circle, and the `ValueError` reaches the caller. The block
receives the document itself (`with undo_group(document, "name") as target:`).

Groups nest, and the whole stack is one undo step. A cancel takes back every open group, so after an inner block that
raised, the outer block finds none left and neither ends nor cancels one: the first exception is the one that arrives.

## Inside GeometryHelper CAD

The same library runs inside the app. GeometryHelper CAD has a PYTHON palette for typing Python, PYRUN for running a file and
PYLOAD for loading a file whose decorated functions become commands, and a script there asks the user for points,
distances and objects through `geometryhelper.cad`, with one undo step for the whole run. It starts a CPython 3.11 to 3.15 of
your own, which Global settings can name; the wheel is not used there, as the app brings its own copy of the package.
[Python in the app](presenter/python.md) is the guide.

## Stubs and an editor

The wheel carries the stubs of the library, so an editor completes the .NET names and checks a script that uses them. They
are a PEP 561 stub package per namespace, `GeometryHelper-stubs` (an `__init__.pyi` for each of the 18 namespaces), and
`System-stubs`, the few BCL classes the library's signatures name, as pythonnet spells them: `System.Array[T]`,
`System.Collections.Generic.List[T]`, `System.Action[...]`. They hold 806 types and 18 559 members (a member is one stub
entry, each overload once), and the summary of each type and member as its docstring, so hovering `GeoPoint3(` in Pylance
shows its overloads and what each does. A class written in several files shows all its summaries joined in the hover.

What the stubs say, so that the editor and pythonnet agree:

- every .NET overload is an `@overload`, and an out parameter is not a parameter but part of the returned tuple;
- a collection a method returns is typed as the .NET interface (`IReadOnlyList[GeoPoint2]`), without `len()` or a slice;
- a parameter that pythonnet takes a Python list for is `Sequence[T] | System.Array[T]`, and one it does not is the .NET
  class only;
- a delegate is `System.Action[...]` or `System.Func[...]`, and an event `+=` takes a plain function;
- a `[Flags]` enum has `|`, `&`, `^` and `~`, a plain enum none;
- the 33 members named as Python keywords are left out, and a parameter named so is `from_`, positional-only;
- two .NET types are written `Any`, `System.Double[,]` and `System.Xml.Linq.XDocument`.

pyright and Pylance find the stubs when the editor uses the virtual environment the wheel is installed in. The one setting
to make: the stubs have no source, because pythonnet makes the module at run time, so every import of one warns
`reportMissingModuleSource`. Turn that rule off, in `pyrightconfig.json`:

```json
{ "reportMissingModuleSource": "none" }
```

or, in VS Code, in `settings.json`: `"python.analysis.diagnosticSeverityOverrides": { "reportMissingModuleSource": "none" }`.
The rest is the checker's own. A script written against what this page says:

```python
text: str = p.DistanceTo(q)                          # Type "float" is not assignable to declared type "str"
p + q                                                # Operator "+" not supported for types "GeoPoint3" and "GeoPoint3"
len(GeoPolygon2(a, b, c).Vertices)                   # "__len__" is not present
DxfEraseFlags.ZeroLengthCurve | 1                    # Operator "|" not supported for types "DxfEraseFlags" and "Literal[1]"
```

each is reported before the script runs. On the test machine pyright 1.1.414 checks the acceptance script in 1.62
to 1.68 s from the command line (five runs), against 0.89 to 0.92 s for a file of one line, so the stubs add about 0.75 s
to a command-line check; an editor's language server reads them once. The package itself, `geometryhelper`, type-checks
with no error in pyright's strict mode, and `py.typed` says its annotations are for use.

Do not install `dotnet-stubs` (from domdfcoding) beside it: it installs a `System-stubs` of its own, and the two clash.

The stubs are written by our own tool, `tools/GeometryHelper.PyStubs` (`bash tools/python/make-stubs.sh`), which reads the
assemblies without running them and the XML documentation, and writes the same bytes on a rerun. It takes ideas from two
MIT-licensed projects and no code: a stub package per namespace, `@overload` for each .NET overload and out parameters as a
tuple from [mcneel/pythonstubs](https://github.com/mcneel/pythonstubs), and the table of .NET base class types from
[MHDante/pythonnet-stub-generator](https://github.com/MHDante/pythonnet-stub-generator).

## What is not supported

- Systems other than 64-bit Windows, and Python outside 3.11 to 3.15 or 32-bit. `pip` refuses the wheel there.
- Installing from PyPI, until the package is published under a name of its own.
- A frozen application (PyInstaller and the like): the package gives no hook, so the assemblies would have to be added
  by hand.
- A module for the presenter outside the app: `geometryhelper.cad` works inside GeometryHelper CAD only (see
  [Inside GeometryHelper CAD](#inside-geometryhelper-cad)), and from outside `GeometryHelper.Cad` is imported as any other
  namespace.

## Where to go next

| Page | What it covers |
|---|---|
| [How do I](howto.md) | a task, the call that does it, and the page that tells the rest: the calls are the ones a script makes |
| [DXF](dxf/index.md), [Reading and writing DWG files](dxf/dwg.md) | the drawing, its tables and entities, read and written |
| [The 2D presenter](presenter/index.md), [Editing as AutoCAD does](presenter/editing.md) | `CadDocument`, its command session and undo |
