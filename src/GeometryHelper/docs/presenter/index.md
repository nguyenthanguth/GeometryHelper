# The 2D presenter

A DXF drawing opened, shown and edited the way AutoCAD does it, by a library that has no window of its own: the document,
what is drawn, picking, the command line, grips, Properties, layers, undo and redo, the owner contract and the command
plugins all run in a test with nothing on the screen. The window is a set of controls over it. Its namespace is
`GeometryHelper.Dxf.Viewer`, and it is not packed yet: the projects are built from the repository.

It opens and edits through [`DxfDatabase`](xref:GeometryHelper.Dxf.DxfDatabase) and nothing else, so what a drawing holds
that the presenter does not draw or edit is kept as the file had it: a drawing opened and saved with no edit is written
back byte for byte. It works in two dimensions only: there is no orbit, no 3D view and no model. A model drawing made by [`GeometryHelper.Drawing`](../drawing/index.md) is shown in it as any other DXF, and its
own entities answer to their [owner](owners.md).

## The projects

| Project | What it holds | Runs on |
|---|---|---|
| `GeometryHelper.Dxf.Viewer` | the document, display lists, picking, the command line and its commands, snaps, grips, Properties, layers, owners, plugins and global settings; no window | netstandard2.0 |
| `GeometryHelper.Dxf.Viewer.Wpf` | the controls: the canvas drawn through SkiaSharp 4.155.0, the file list, Properties, the layer box, the command line and the status bar | net48 and net8.0-windows |
| `GeometryHelper.Dxf.App` | GeometryHelper DXF, the presenter's own app, `GeometryHelper.Dxf.App.exe plan.dxf` (see [The apps](app.md#the-apps)) | net8.0-windows |
| `GeometryHelper.Drawing.App` | GeometryHelper Drawing, the app with the model drawing added, `GeometryHelper.Drawing.App.exe plan.dxf` | net8.0-windows |

The core is the part to build on: it is what a test drives, and what the controls call. The controls and the window are
not in the API reference; the pages here say what they do where it matters.

## A first look

A [`CadDocument`](xref:GeometryHelper.Dxf.Viewer.CadDocument) opened, drawn into a display list, picked in and edited through its
[`CadCommandSession`](xref:GeometryHelper.Dxf.Viewer.CadCommandSession):

```csharp
using GeometryHelper.Dxf.Viewer;

CadDocument document = CadDocument.Open("plate.dxf");              // a plate 100 by 60 with a hole of 20 and a TEXT
DisplayList2 display = document.GetDisplay(document.ModelSpace);   // 6 items: four lines, a circle, a text
PickIndex index = document.GetPickIndex(document.ModelSpace);

IReadOnlyList<string> hits = index.Pick(50, 40.5, 2);              // the circle: 0.5 off its rim, within 2
document.Selection.Set(hits);

var session = new CadCommandSession(document);                     // the command line of this drawing
session.Feed("L");                                                 // Command: L, which starts LINE
session.Feed("0,100");
session.Feed("@50<0");
session.Feed("@0,20");
session.Enter();                                                   // two segments, one undo step

// document.EntityCount is 8 now; session.Feed("U") makes it 6 again, and the file is as it was.
```

What each line does is on the page that follows.

## The pages

| Page | What it covers |
|---|---|
| [Opening, drawing and picking](display.md) | the document and its spaces, what each entity is drawn as, the view and its zoom, [`PickIndex`](xref:GeometryHelper.Dxf.Viewer.PickIndex), the selection |
| [Editing as AutoCAD does](editing.md) | one edit, one undo step; the command line, its aliases and its commands; object snaps; grips; Properties; layers; saving |
| [Plotting to PDF](plot.md) | the page setup a layout keeps, the sheet a plot is laid on, the plot style tables, the PDF, PLOT, -PLOT, PREVIEW and PAGESETUP |
| [Owners](owners.md) | the entities a model drawing wrote, and how the presenter asks their owner before it changes one |
| [Command plugins](plugins.md) | a command written in a .dll, loaded and unloaded while the app runs |
| [Settings, autosave and the window](app.md) | global settings as JSON, autosave copies, closing a drawing, the keys, the mouse and the app's command line |

## Measured

On a drawing of 99 856 lines in 10.6 MB, on .NET 8:

| Step | Time |
|---|---|
| `CadDocument.Open(path)` | 36 ms |
| the display list of model space, 99 856 items, the first time it is asked for | 437 ms |
| the `PickIndex` over it | 19 ms |
| one `Pick` at a point, averaged over 1 000 | 0.002 ms |
| `Window` over a box of 100 by 100, which takes 100 entities | 0.56 ms |
| `ItemsNear` for a box 100 by 100, which meets 121 items | 0.33 ms |

An XLINE or a RAY has no end, so it is kept out of the grid and tried only against the box asked about, grown by its own
size: one that passes further off is never handed to a pick or a snap. On a drawing of 100 000 lines, arcs and circles
with 200 xlines and rays among them, 20 000 snaps take 40 ms and 20 000 picks 20 ms on .NET 8, against 22 to 28 ms and
14 to 19 ms with no xline at all; when every xline was handed to every pick they took 119 to 136 ms and 73 to 98 ms.

The display list is made once and kept until an edit touches the space, so panning and zooming afterwards cost only the
drawing of what is on the screen. An edit, an undo or a redo then draws again only what it changed: the entities its
records name, those that read a layer it switched off, froze or recoloured, and those that share a dimension's picture
or a leader's words with them; every other entity keeps its items, and the list is the one a REGEN would make, item for
item. A change of a table, a block or LTSCALE draws every entity of the spaces it touched, as before.

On a drawing of a million entities (lines, polylines, arcs, circles, TEXT and MTEXT in SHX and TrueType fonts, hatches,
dimensions, and 1 000 INSERTs of a block of 500 entities: 3.9 million items), on .NET 8:

| Step | Time |
|---|---|
| the display list of model space, the first time | 10.3 s |
| the `PickIndex` over it | 0.26 s |
| a frame of the whole drawing, 1 600 by 1 000 pixels | 0.76 s |
| a frame zoomed in 30 times | 6.6 ms |
| a line moved: the edit, kept for undo | 23 ms |
| the same, drawn again with its `PickIndex` | 0.46 s |
| its undo: the drawing read back, then drawn again | 2.3 s, then 0.45 s |
| a layer of a third of the entities switched off, drawn again | 0.53 s |

A step of the undo list is saved at once and compressed on a worker thread, so an edit does not wait for the 140 MB of
such a drawing to be packed; an undo reads the whole drawing back, which is the 2.3 s above.

## See also

- [DXF files](../dxf/index.md): the model the presenter opens and edits.
- [Model drawings](../drawing/index.md): drawings made from a 3D model, shown here.
- [Editing rules and what the model does not do](../dxf/rules.md): what each entity accepts.
