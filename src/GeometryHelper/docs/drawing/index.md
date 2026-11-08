# Model drawings

A drawing of a 3D model, in the manner of Tekla Structures': views along the axes with the hidden lines left out exactly,
sections and details, dimensions chosen by rule, marks that keep clear of the lines, centre lines, weld symbols, a sheet
with its title block and part list, all written into one DXF that carries its own document, so that it opens again with its
settings and can be made again when the model changes, the hand edits kept. The namespace is `GeometryHelper.Drawing`, with
`GeometryHelper.Drawing.Annotation` and `GeometryHelper.Drawing.Projection` under it; it is not packed yet.

It builds on [the 2D presenter](../presenter/index.md) and the presenter does not know it: a model drawing is a
[`DxfDatabase`](xref:GeometryHelper.Dxf.DxfDatabase) written through the DXF model, shown in the presenter as any DXF, and its
entities are held by an [owner](../presenter/owners.md) that is part of this module.

## The way through

```
DrawingSource  ->  views  ->  lines of each view  ->  dimensions, marks, symbols  ->  sheet  ->  DXF + document
(objects)         (frames)    (hidden-line removal)    (by rule)                      (scale)     (GH_DRAWING)
```

| Step | Type | Page |
|---|---|---|
| the objects drawn, each a body with a kind, a frame and texts | [`DrawingSource`](xref:GeometryHelper.Drawing.DrawingSource), [`DrawingObject`](xref:GeometryHelper.Drawing.DrawingObject) | [Sources](source.md) |
| where each view looks from, its crop and its scale | [`DrawingView`](xref:GeometryHelper.Drawing.DrawingView), [`ViewFrame3`](xref:GeometryHelper.Drawing.ViewFrame3), [`ViewBuilder`](xref:GeometryHelper.Drawing.ViewBuilder) | [Views, projection, sections and details](views.md) |
| the lines seen and hidden, the faces a section cuts | [`ViewProjector`](xref:GeometryHelper.Drawing.Projection.ViewProjector) | [Views](views.md#projection) |
| dimensions, marks, centre lines, weld, section, detail and level marks | [`Annotator`](xref:GeometryHelper.Drawing.Annotation.Annotator), [`Marker`](xref:GeometryHelper.Drawing.Annotation.Marker) | [Dimensions, marks and symbols](annotation.md) |
| the settings, the scale, the sheet and its title block | [`DrawingSettings`](xref:GeometryHelper.Drawing.DrawingSettings), [`DrawingBuilder`](xref:GeometryHelper.Drawing.DrawingBuilder) | [Settings, scale and sheet](settings.md) |
| the drawing made, saved, opened, shown and made again | [`Drawing`](xref:GeometryHelper.Drawing.Drawing), [`UpdateReport`](xref:GeometryHelper.Drawing.UpdateReport) | [Making it again](update.md) |
| what is kept in the DXF, and who owns its entities | [`DrawingDocumentStore`](xref:GeometryHelper.Drawing.DrawingDocumentStore), [`DrawingOwner`](xref:GeometryHelper.Drawing.DrawingOwner) | [The document in the DXF](document.md) |

## A first look

A beam 3 000 long, an I-section 290 deep and 300 wide, with an end plate 300 by 340 by 20 at its far end and four holes of 22
in the plate, is a source of two objects (the code that builds it is on [Sources](source.md#a-source-in-code)). Drawn with
the assembly preset on an A3:

```csharp
using GeometryHelper;
using GeometryHelper.Drawing;
using GeometryHelper.Packing;

DrawingSource source = BeamWithEndPlate();                       // B1, the beam (the main part), and P1, the plate
DrawingSettings settings = DrawingSettings.Assembly;
settings.Sheet.Size = PaperSize.A3;

Drawing drawing = DrawingBuilder.Build(source, settings, Tolerance.Default);   // 0.2 s
drawing.Save("beam.dxf");                                        // one DXF, 42 091 bytes
```

| What | Value |
|---|---|
| the drawing's id | `B1`, the main object's |
| the scale and the sheet | 1:10 on an A3 of 420 by 297 |
| views | `V-FRONT`, `V-TOP`, `V-RIGHT` |
| the front view | 13 visible and 4 hidden lines; the overall dimensions 3000 along and 290 up; one mark, `P1`, on the end plate |
| the top view | 7 visible and 6 hidden lines; overall dimensions 3000 and 300 |
| the right view | 8 visible and 8 hidden lines; the plate's four holes with centre lines; no dimension |
| the part list | `B1 1 HEA300 S355 3000 BEAM`, `P1 1 PL20*300 S355 300 END PLATE` |
| the document | 16 elements: 3 views, 4 dimensions, 1 mark, 4 symbols, the settings, the sheet, the title block, the part list |
| the DXF | model space of 57 entities, a layout "Sheet" with 4 viewports, 95 entities tagged as the drawing's |

The plate's holes are not in the front view, which sees the plate edge-on, so the right view is where they are, with their
centre lines; the assembly preset dimensions the front and top views and not the right.

## What there is not

- **No drawing plugins yet.** The document records the runs of a plugin (`Drawing.PluginRuns`) and an update reports them
  (`UpdateReport.PluginRunsToRunAgain`), but nothing runs them: the plugin API for drawings is not written.
- **No AWS A2.4 weld symbols.** Asking for them throws `NotSupportedException`; ISO 2553 is drawn.
- **No circular crop** of a detail: it is cut out by a rectangle.
- **No 3D.** The model is used inside the core, to be projected into lines; what is shown is flat.
- **Tekla only through its bridge**, which reads a model through Tekla's Open API and never changes it; see
  [Sources](source.md#reading-tekla-structures).

## The app

`GeometryHelper.Drawing.Wpf` adds the model drawing to the presenter's window through its extension seam, and is not in the
API reference. `AddModelDrawings(model, sources, tolerance)` puts a Drawing tab on the ribbon, the dialogs New drawing,
Update from model, Drawing settings and Export, and a row for each model drawing in the file list, with a dot that is amber
while the model has changed since it was made, blue while it has hand edits or changes not saved, and green otherwise. A
source is an `IDrawingSourceProvider`: a name, a line saying what it gives, whether it can give objects now, and the
objects, in model millimetres, each with an id the same from one call to the next. The app offers the test bodies (a beam with
end plates, a column base with bolts, a small frame), the test bodies changed to try Update from model, and Tekla Structures
and IFC, listed and greyed until a bridge is connected. The commands VIEWSECTION and VIEWDETAIL cut a section along a line
drawn in a view and a detail of a rectangle, as [`AddSection` and `AddDetail`](update.md#adding-and-changing-one-view) do.

A drawing made in the app is saved at once, once the global settings are applied, as the app does when it starts: into the save
folder of [Global settings](../presenter/app.md#global-settings), or where that is empty into a folder "GeometryHelper
Drawing" in the user's Documents, made where it is not there. It is named by its number, `B-101.dxf`, or `B-101 (2).dxf` where a
file of that name is there already, so that no file is written over. It then has a file, which Save writes to and autosave
keeps copies beside; a folder that cannot be made or written to is said on the command line and the drawing stays without a
file, to be saved with Save As.

## The pages

| Page | What it covers |
|---|---|
| [Sources](source.md) | `DrawingObject`, `DrawingSource`, the frame the views are set in, a source built in code, and reading Tekla Structures |
| [Views, projection, sections and details](views.md) | where a view looks from, hidden-line removal, sections along an axis and along a line, details |
| [Dimensions, marks and symbols](annotation.md) | the rules that dimension a view, how marks are placed, centre lines, welds, section, detail and level marks |
| [Settings, scale and sheet](settings.md) | the presets and every setting, how the scale and the sheet are chosen, how the DXF is laid out |
| [Making it again](update.md) | `Save`, `Open`, `Update`, `Prepare` and `Apply`, one view at a time, hand edits kept and dropped |
| [The document in the DXF](document.md) | what is stored inside the file, the tags on its entities, and `DrawingOwner` |

## See also

- [The 2D presenter](../presenter/index.md): where a model drawing is shown and edited.
- [Packing boxes onto sheets](../packing.md): how the views are placed on the sheet.
- [Label placement](../arrange.md): how the marks are kept clear of each other.
- [DXF files](../dxf/index.md): what the drawing is written as.
