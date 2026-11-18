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

## See also

- [The 2D presenter](index.md) for the projects and the command line the channel types on.
- [Owners](owners.md) for the refusals an owner makes of an edit.
- [Settings, autosave and the window](app.md) for Global settings.
