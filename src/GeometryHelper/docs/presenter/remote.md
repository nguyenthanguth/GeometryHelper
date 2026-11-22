# Driving the app from another program

Another program can read and edit the drawings of a running GeometryHelper CAD, run commands on its command line, ask
the user to pick a point or objects on the canvas and save, in the way a Tekla Open API program drives a running Tekla
Structures. The client is the project `GeometryHelper.Cad.Remote` (netstandard2.0, referencing only GeometryHelper, so a
Tekla 2020 plugin on .NET Framework 4.8 can use it as well as a console program or a tool on .NET 8). Nothing is
installed on the app's side: the app opens the channel when it starts, and Global settings can close it
([the setting](#the-setting)).

Entities travel as DXF text, so the program works with the library's own types, `DxfLine`, `DxfPolyline`, `DxfMText`,
`DxfDimension`, `DxfBlockReference` and the rest, and nothing new has to be learnt: what a program reads it can edit, and
what it makes in code it can put in the drawing.

## Beside Tekla's Open API

| In Tekla Structures | Here | What differs |
|---|---|---|
| `new Model()` | [`new CadApplication()`](xref:GeometryHelper.Cad.Remote.CadApplication) | connects to the app the user activated last; `CadApplication.GetRunning()` lists the apps |
| `model.GetConnectionStatus()` | the constructor throws [`CadRemoteException`](xref:GeometryHelper.Cad.Remote.CadRemoteException) when no app takes it | |
| the model open | `app.ActiveDocument`, `app.Documents`, `app.Open(path)`, `app.New()` | an app holds several drawings; each is a [`CadRemoteDocument`](xref:GeometryHelper.Cad.Remote.CadRemoteDocument) |
| `beam.Insert()` | `drawing.Insert(entity)` | queued; returns a ticket such as `~1` until the commit |
| `beam.Modify()` | `drawing.Modify(entity)` | queued, on an object read from the drawing |
| `beam.Delete()` | `drawing.Delete(handle)` or `Delete(entity)` | queued |
| `model.CommitChanges()` | `drawing.CommitChanges()` | applies everything queued as one undo step, `REMOTE` |
| `ModelObjectSelector` | `drawing.GetObjects(filter)`, `GetObject(handle)`, `GetSelectedObjects()` | copies, not live objects |
| `Picker.PickPoint(prompt)` | `drawing.PickPoint(prompt)` | waits for the user; Esc throws with `IsCancelled` |
| `Picker.PickObjects(...)` | `drawing.PickObjects(prompt)`, `PickObject(prompt)` | |
| `Operation.RunMacro` | `drawing.SendCommand("ZOOM E ")` | types on the command line |

## Which app and which drawing

`new CadApplication()` connects to the app whose window the user activated last. When two apps are open,
`CadApplication.GetRunning()` lists them, the one activated last first, each with its process id, its window's title and the
drawings it has open, and `new CadApplication(running)` connects to the one chosen; `CadApplication.ForDocument(path)`
finds the app that has a file open.

Every open drawing has an id that holds as long as the app runs. A `CadRemoteDocument` always means its own drawing, however
the user switches tabs meanwhile: a drawing the user has closed makes the next call throw
`The drawing A-101.dwg is no longer open.`, and no call ever falls through to another drawing. `app.Documents` lists the
drawings (`Id`, `Name`, `Path`, `IsActive`), `app.Documents.Get("A-101.dwg")` finds one by its path or name, `app.Open(path)`
returns the drawing at once where the app has the file open, and `drawing.Activate()` shows it as clicking its tab does.
A request to a drawing that is not shown does not show it, except the ones that work on what the user sees: a command, a
pick and the switch of a layout.

## Reading and writing

Reads are live and give copies. `GetObject(handle)`, `GetObjects(new CadObjectFilter { Kind = typeof(DxfLine), Layer = "Steel" })`
and `GetSelectedObjects()` ask the app now and return the library's entities made from the objects' DXF; changing one changes
nothing in the app. The `Handle` of a copy is its handle in the copy's own small drawing, so `drawing.HandleOf(entity)` gives
its handle in the app's drawing.

Writes are queued. `Insert`, `Modify`, `Delete`, `AddLayer` and `ModifyLayer` keep what they were given as it is at the call,
and `CommitChanges()` sends the lot:

- the app makes the layers first, then applies the objects in the order they were queued, **as one undo step** named
  `REMOTE`, and draws the canvas again; one `U` takes the whole commit back, layers included;
- it applies all or nothing. If any change is refused, nothing is applied, the queue is emptied and `CommitChanges` throws with
  the reason: a layer that exists, an object that is gone, a command running in the drawing, or an
  [owner](owners.md) that does not take the change (a dimension of a model drawing, say) with its own words;
- the handles come back in the order of the `Insert` calls. Until then `Insert` returns a ticket (`~1`, `~2`...) that stands
  for the object in `Delete`, `GetObject` and `Select` once the commit is done.

A layer the drawing lacks is made when an object on it is inserted, white and continuous; `AddLayer` makes it the way the
program wants. A dimension made in code is drawn by the app with the drawing's current dimension style, unless it names one.
A hatch, an MTEXT and a block reference with attributes travel by value and come back as they went.

## Blocks the program defines

An INSERT made from a block of the program's own, in a `DxfDatabase` it holds, brings the block's definition with it when the
drawing lacks the block:

```csharp
var mine = new DxfDatabase("AC1027");
var bolt = new DxfBlockTableRecord { Name = "BOLT" };
mine.BlockTable.Add(bolt);
bolt.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 5.0));
bolt.AppendEntity(new DxfLine(new GeoPoint3(-5, 0, 0), new GeoPoint3(5, 0, 0)));

drawing.Insert(new DxfBlockReference(new GeoPoint3(100, 100, 0), bolt));
drawing.Insert(new DxfBlockReference(new GeoPoint3(200, 100, 0), bolt));   // the same block, made once
drawing.CommitChanges();                                                    // BOLT is now in the drawing's block table
```

Three rules. A block the drawing has is the drawing's own, whatever the program's block of that name holds, as AutoCAD binds
a block by its name. An INSERT read from another drawing of the same app brings its block the same way. And an INSERT of a
block that is neither in the drawing nor defined with at least one object is refused with the reason, nothing applied.

## Layers and layouts

`drawing.Layers` lists the layers in the order of the table, each a [`CadRemoteLayer`](xref:GeometryHelper.Cad.Remote.CadRemoteLayer)
with `Name`, `IsOn`, `IsFrozen`, `IsLocked`, `IsPlottable`, `Color` (an AutoCAD index or a true colour), `Linetype`,
`LineWeight` and `Description`. They are copies, asked of the app each time.

```csharp
drawing.AddLayer(new CadRemoteLayer("Bolts") { Color = DxfColor.FromRgb(10, 200, 30), Description = "Bolt marks" });
drawing.Insert(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(50, 0, 0)) { Layer = "Bolts" });

CadRemoteLayer steel = drawing.Layers.Single(l => l.Name == "Steel");
steel.IsLocked = true;
drawing.ModifyLayer(steel);

drawing.CommitChanges();     // one undo step: the layer made, the line on it, Steel locked
```

The app refuses, with nothing applied: a name AutoCAD does not take for a layer (`A<B`) or a layer of that name already (either
case), a linetype the drawing does not list, a layer to modify that is not there, a layer of an external reference
(`Base|Steel`) and the freezing of the current layer.

`drawing.CurrentLayer` reads and sets the layer new objects are drawn on, as the layer box does; setting a frozen layer is
refused. `drawing.Layouts` lists the tabs in the order they are shown (`Model`, `Layout1`...) and `drawing.CurrentLayout` reads
and sets the tab shown, as clicking it does: the switch is an undo step of its own (`LAYOUT`), the drawing is shown first, and
a command running in it refuses it. `GetObjects(new CadObjectFilter { Layout = "Layout1" })` reads a layout's objects, and
`Insert(entity, "Layout1")` puts one there.

## Commands

`drawing.SendCommand("ZOOM E ")` types on the drawing's command line as the user does, words parted by spaces going in one by
one, and returns when the command ends or after 30 seconds, with what the command line wrote. The drawing is shown first,
since a command works on the drawing the user sees. A command that asks the user for a point stays running after the time,
the result says so, and the command goes on in the app; type the answer with another `SendCommand`, or let the user give it.
`drawing.Save()` and `drawing.SaveAs(path, version)` write the file (`SaveAs("A-101.dwg", "AC1024")` writes a DWG of
AutoCAD 2010); `SaveAs` makes the new path the drawing's name from then on.

## Asking the user to pick

A pick starts a command of its own on the drawing's command line. The drawing is shown, the prompt appears, and the
user answers as they answer any command: a click on the canvas with the snaps on, a point typed, a window drag, Enter.

```csharp
GeoPoint3 corner = drawing.PickPoint("Pick the corner of the frame");
GeoPoint3 end = drawing.PickPoint("Pick the end", corner);        // a rubber band from the corner
IReadOnlyList<DxfEntity> bars = drawing.PickObjects("Select the bars to dimension");
DxfEntity plate = drawing.PickObject("Select the plate");
```

- `PickPoint` gives a point in world coordinates, its Z nought (the presenter draws in two dimensions).
- `PickObjects` asks "Select objects:" until Enter, with every way the user selects: click, window, crossing. Objects the user
  had selected before the call are taken at once, as the app's commands take them (and the selection is cleared when the
  pick ends). The objects come back in the order of their handles, as copies that `HandleOf` and `Modify` work on.
- `PickObject` ends on one click; if exactly one object was selected before, it is taken at once.
- **Esc** ends the pick and the call throws `CadRemoteException` with `IsCancelled` true. So does Enter at `PickPoint`.
  Every other failure has `IsCancelled` false.
- An optional time (`PickPoint(prompt, null, TimeSpan.FromSeconds(30))`): when it is up the call throws
  `The user did not pick in time`, and the pick is ended in the app with Esc. The default is no limit.
- A command already running in the drawing is not interrupted: the pick throws `busy with the command LINE`. End the command,
  or send it Esc with `SendCommand`.

While a pick waits, the app is free: its window answers, the user can pan, zoom and use the ribbon, and nothing is locked on
the UI thread. The server looks every 20 milliseconds whether the pick has ended, and each look is a short piece of work on
the UI thread of its own. **Another program that asks the app meanwhile is not made to wait:** it is told at once
`GeometryHelper CAD is busy: it is waiting for the user to pick in a drawing, for a program that asked it to`, and its
`CadApplication` is still good for the next request; `CadApplication.GetRunning()` lists such an app with no drawings.

## The setting

Global settings › Save and export › Other programs: **Allow other programs to connect**, on by default and kept in the settings
file as `"remote": { "allowRemoteConnections": true }`. It is applied when the settings are, with no restart:

- off: the app closes its pipe, deletes its instance file and drops the connections it has; no program finds it, and a program
  that tries gets `No GeometryHelper CAD is running. Start the app, or check that it allows other programs to connect.`;
- on: the app opens a new pipe and writes the file again.

The line under the switch says which: `Other programs can connect: GeometryHelper.Cad.12840` or `Other programs cannot connect.`

## Security

The pipe is `GeometryHelper.Cad.<process id>`, open to the Windows user who runs the app and to nobody else, and to this computer
only: other users on the machine and other machines cannot connect. Any program that user runs can drive the app, as any
program can drive the user's Tekla; turn the setting off where that is not wanted. The app announces itself in
`%LocalAppData%\GeometryHelper\Remote\<process id>.json` (process id, window title, time last activated) and deletes the file
when it stops; a file left by a process that died is ignored and removed by the next client that finds it. No new package
is used, and nothing is installed on the machine.

## Errors

Everything the app or the channel refuses is a `CadRemoteException` with the reason in the words the app's own command line
would use: no app running, the connection lost, a drawing closed, an owner's refusal, a block that is not there, a busy command,
a busy app, a cancelled pick. An argument that is wrong in the caller's own code (`null`, a negative time) is the usual
`ArgumentNullException` or `ArgumentOutOfRangeException`, thrown before anything is sent.

## Threads

A `CadApplication` is for one thread at a time: its requests go one after the other on one pipe, and a second thread that calls it
waits for the first, a pick included. Use one `CadApplication` for each thread that needs the app at once; the app then serves
them one at a time, and tells a client that asks during a pick that it is busy.

The app runs each request on its UI thread, between two events of the window; a long command (a big hatch, a regen) holds the
next request until it ends, as it holds the user.

## A full example

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Cad.Remote;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

using (var app = new CadApplication())                         // the app the user worked in last
{
    CadRemoteDocument drawing = app.ActiveDocument ?? app.New();

    // A layer, a framed title and a line on it, as one undo step.
    drawing.AddLayer(new CadRemoteLayer("SAMPLE") { Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 3) });

    var frame = new DxfPolyline { Layer = "SAMPLE" };                         // 200 by 60
    frame.AddVertexAt(0, new GeoPoint2(0, 0), 0, 0, 0);
    frame.AddVertexAt(1, new GeoPoint2(200, 0), 0, 0, 0);
    frame.AddVertexAt(2, new GeoPoint2(200, 60), 0, 0, 0);
    frame.AddVertexAt(3, new GeoPoint2(0, 60), 0, 0, 0);
    frame.Closed = true;
    drawing.Insert(frame);
    drawing.Insert(new DxfLine(new GeoPoint3(10, 35, 0), new GeoPoint3(190, 35, 0)) { Layer = "SAMPLE" });
    drawing.Insert(new DxfMText { Contents = "Title", Location = new GeoPoint3(10, 50, 0), TextHeight = 8, Layer = "SAMPLE" });
    IReadOnlyList<string> handles = drawing.CommitChanges();                  // 3 handles

    // The user picks where the circle goes.
    try
    {
        GeoPoint3 centre = drawing.PickPoint("Pick the centre of the circle");
        drawing.Insert(new DxfCircle(centre, new GeoVector3(0, 0, 1), 25) { Layer = "SAMPLE" });
        drawing.CommitChanges();
    }
    catch (CadRemoteException ex) when (ex.IsCancelled)
    {
        Console.WriteLine("Cancelled.");
    }

    drawing.SendCommand("ZOOM E ");
}
```

The same program is in the repository as `tests/GeometryHelper.Cad.Remote.Sample`, and the remote tests run its code against a
headless app with the pick answered by the test. To try it by hand, open GeometryHelper CAD with a drawing, run
`dotnet run --project tests/GeometryHelper.Cad.Remote.Sample`, and click a point in the drawing when the command line asks
`Pick the centre of the circle:`.

## From a Tekla plugin

A Tekla plugin or macro on .NET Framework 4.8 references `GeometryHelper.Cad.Remote.dll` and `GeometryHelper.dll` (the client has
no other dependency) and draws what it finds in the model into the drawing the user has open. The code below is the shape of it,
for Tekla 2020, 2025 and 2026 alike, since the client does not touch Tekla's assemblies; it has not been run against Tekla.

```csharp
using System.Collections.Generic;
using GeometryHelper.Cad.Remote;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Tekla.Structures.Model;

public static class BeamsToCad
{
    /// <summary>Draws the centre line of each selected beam in the plan of the drawing the user has open in GeometryHelper CAD.</summary>
    public static int Send()
    {
        var selected = new Tekla.Structures.Model.UI.ModelObjectSelector().GetSelectedObjects();
        using (var app = new CadApplication())
        {
            CadRemoteDocument drawing = app.ActiveDocument;
            drawing.AddLayer(new CadRemoteLayer("TEKLA-BEAMS"));

            while (selected.MoveNext())
            {
                if (selected.Current is Beam beam)
                {
                    drawing.Insert(new DxfLine(
                        new GeoPoint3(beam.StartPoint.X, beam.StartPoint.Y, 0),
                        new GeoPoint3(beam.EndPoint.X, beam.EndPoint.Y, 0)) { Layer = "TEKLA-BEAMS" });
                }
            }

            return drawing.CommitChanges().Count;      // one undo step in the CAD for all of them
        }
    }
}
```

## From Python

A Python script drives the app with the same client, [`CadApplication`](xref:GeometryHelper.Cad.Remote.CadApplication), run by
pythonnet from the `geometryhelper` wheel ([Installing](../python.md#installing)). The names are the .NET ones and the calls
are those of the sections above; no Python class wraps the client, so nothing here is learnt twice. It needs Windows, the
Windows user who runs the app (the pipe admits nobody else), and [the setting](#the-setting) on.

```python
import geometryhelper
from System import Array, String, TimeSpan
from GeometryHelper.Cad.Remote import CadApplication, CadObjectFilter, CadRemoteException, CadRemoteLayer
from GeometryHelper.Dxf import DxfColor, DxfColorMethod, DxfLine, DxfMText
from GeometryHelper.Geometry import GeoPoint3

with CadApplication() as app:                              # the app the user worked in last; Dispose at the block's end
    drawing = app.ActiveDocument
    if drawing is None:
        drawing = app.New()

    layer = CadRemoteLayer("SAMPLE")
    layer.Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 3)
    drawing.AddLayer(layer)
    for x in range(0, 100, 10):                            # ten vertical lines, 50 long, 10 apart
        line = DxfLine(GeoPoint3(x, 0, 0), GeoPoint3(x, 50, 0))
        line.Layer = "SAMPLE"
        drawing.Insert(line)
    title = DxfMText()
    title.Contents = "Title"
    title.Location = GeoPoint3(0, 60, 0)
    title.TextHeight = 8
    title.Layer = "SAMPLE"
    drawing.Insert(title)
    handles = drawing.CommitChanges()                      # one undo step
    print(handles.Count)                                   # 11

    only_lines = CadObjectFilter()                         # no CadObjectFilter(Layer="SAMPLE"): set the properties after
    only_lines.Kind = DxfLine                              # a Python class stands for the .NET Type
    only_lines.Layer = "SAMPLE"
    print(drawing.GetObjects(only_lines).Count)            # 10

    drawing.Select(Array[String](list(handles)[:3]))       # an array of strings, not a Python list
    print(drawing.GetSelectedObjects().Count)              # 3

    result = drawing.SendCommand("ZOOM E\n")               # "\n" is Enter
    print(result.Completed)                                # True

    try:
        centre = drawing.PickPoint("Pick the centre", timeout=TimeSpan.FromSeconds(30))
        print(centre)
    except CadRemoteException as error:
        print("cancelled" if error.IsCancelled else error.Message)

    drawing.SaveAs("sample.dxf")
```

The script runs here against a headless app in a process of its own, with the pick answered by typing `120,80` on its command
line: it prints `11`, `10`, `3`, `True` and `(120, 80, 0)` in turn, on .NET 8 and on .NET Framework 4.8 alike, and
`sample.dxf` is a file of 6 046 bytes. In the app the `ZOOM E` shows the drawing's extents; the headless app has no window,
so the zoom moves a view of its own that nothing shows (the one `-VIEW Save` keeps), and answers `Completed`.

Where Python differs from the C# of the sections above, measured on .NET 8 and on .NET Framework 4.8 with the same result:

- **`with` closes the connection.** pythonnet makes every .NET type that is `IDisposable` a context manager, so
  `with CadApplication() as app:` calls `Dispose`, which closes the pipe, at the end of the block and also when the block
  raises: a `ValueError("boom")` raised inside came out of the block as it was. Without `with`, call `app.Dispose()`. A call
  made on the object after `Dispose` opens a new pipe without a word, as a lost connection is opened again, so leave the
  object alone once the block has ended.
- **A collection argument is an array.** `drawing.Select(["1A", "1B"])` is a `TypeError: No method matches given arguments
  for Select: (<class 'list'>)`, since `Select` takes an `IEnumerable<string>` and pythonnet converts a Python list only to
  an array. Write `Select(Array[String](["1A", "1B"]))`, or a `List[String]()` filled by `Add` (`List[String](["1A"])` is
  refused as well); `Select(Array[String]([]))` selects nothing. The stubs say so: an editor marks `Select(["1A"])` before
  the script runs ([Lists: only for arrays](../python.md#lists-only-for-arrays)).
- **Set a filter's properties after making it.** `CadObjectFilter(Layer="SAMPLE")` is a `TypeError`, because pythonnet
  has no keyword initialiser for a property, only for a constructor's own arguments (`GeoPoint3(x=1, y=2, z=3)` works).
  Make the filter, then assign. `Kind` takes the Python class itself (`only_lines.Kind = DxfLine`) or
  `clr.GetClrType(DxfLine)`.
- **A command ends on Enter.** `SendCommand("LINE 0,0 100,0\n\n")` draws the line and ends, and its `Output` holds the
  prompts with the answers (`Command: LINE`, `Specify first point: 0,0`, `Specify next point or [Undo]: 100,0`). The same
  text ending in one space leaves LINE waiting for another point, and the call returns after its time (30 seconds when
  none is given) with `Completed` false. Give a shorter time to leave a command open on purpose:
  `SendCommand("LINE 0,0", TimeSpan.FromMilliseconds(200))` returns at once with `Completed` false, and the next
  `SendCommand("100,50\n\n")` finishes it.
- **Ctrl+C waits for the call.** Python takes the interrupt between two of its own instructions, and a call into .NET is one
  instruction. A pick with no time limit therefore goes on until the user answers in the app, and a
  `KeyboardInterrupt` raised at 1 second of a `PickPoint(..., timeout=TimeSpan.FromSeconds(4))` came at 4 seconds, when the
  call returned with its timeout exception. In a script, give every pick and every long command a `timeout`.

What it costs, on this machine with the app in another process, and a drawing of 10 000 lines:

| | .NET 8 | .NET Framework 4.8 |
|---|---|---|
| starting the runtime and the three assemblies, once per script | 211 to 224 ms | 282 to 288 ms |
| connecting and the hello that checks the version | 7.7 to 8.3 ms | 18.9 to 19.5 ms |
| one small request (the number of open drawings) | 26 to 43 us | 37 us |
| `GetObjects()` of the 10 000 lines, as `DxfLine` objects | 315 to 348 ms | 298 to 304 ms |
| 1 000 `Insert` of a line, queued, then their `CommitChanges` | 91 to 93 ms, then 38 to 50 ms | 107 to 144 ms, then 55 to 69 ms |
| `SendCommand("LINE 0,0 100,0\n\n")` | 16.9 ms | 17.5 to 19.4 ms |

Most of the 10 000-line read is the app's, which writes the drawing as DXF text: a client that took only that text, with no
Python or .NET types made from it, waited 260 to 281 ms for it.

**A script that runs inside the app does not connect to that app.** The Python of PYRUN, PYLOAD and the PYTHON palette
runs on the app's own UI thread, and a request from it to the same app would wait for that thread, which is the script
itself, for good. The app refuses such a connection before anything is sent, with a `CadRemoteException`:
`A script inside GeometryHelper CAD cannot drive the same app through CadApplication: use geometryhelper.cad, which
reaches the drawing directly.` Inside the app, use `geometryhelper.cad` ([Python in the app](python.md));
`CadApplication` is for a script that runs outside it.

Three scripts that drive the app, one that draws a grid and reads it back, one that waits for a pick and one that lists the
open drawings, are in [Python examples](../python-examples.md#driving-a-running-app).

## See also

- [The 2D presenter](index.md) for the projects and the command line the channel types on.
- [Owners](owners.md) for the refusals an owner makes of an edit.
- [Settings, autosave and the window](app.md) for Global settings.
